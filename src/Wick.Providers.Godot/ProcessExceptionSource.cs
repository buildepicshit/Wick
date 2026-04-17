using System.Diagnostics;
using System.Runtime.CompilerServices;
using Wick.Core;

namespace Wick.Providers.Godot;

/// <summary>
/// Captures C# exceptions from a Godot game process by reading its stderr.
/// This is the Tier 1 "agent-launched" capture path — Wick spawns the game
/// and gets full exception output with stack traces directly from stderr.
/// </summary>
public sealed class ProcessExceptionSource : IExceptionSource
{
    private readonly string _godotBinaryPath;
    private readonly string _projectPath;
    private readonly string? _scenePath;
    private readonly LogBuffer? _logBuffer;
    private readonly Action<int>? _onHandshake;

    public ProcessExceptionSource(
        string godotBinaryPath,
        string projectPath,
        string? scenePath = null,
        LogBuffer? logBuffer = null,
        Action<int>? onHandshake = null)
    {
        _godotBinaryPath = godotBinaryPath;
        _projectPath = projectPath;
        _scenePath = scenePath;
        _logBuffer = logBuffer;
        _onHandshake = onHandshake;
    }

    public async IAsyncEnumerable<RawException> CaptureAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _godotBinaryPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Build arguments: --headless --path <project> [scene]
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--path");
        startInfo.ArgumentList.Add(_projectPath);
        if (!string.IsNullOrEmpty(_scenePath))
        {
            startInfo.ArgumentList.Add(_scenePath);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Wick] Failed to start Godot process: {ex.Message}");
            yield break;
        }

        // Kill process on cancellation
        using var registration = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (InvalidOperationException) { /* already exited */ }
        });

        // Read stderr line by line, accumulate error blocks, parse
        var errorBlock = new List<string>();
        var inErrorBlock = false;

        await foreach (var line in ReadLinesAsync(process.StandardError, ct))
        {
            // Wick envelope path: if the line is a Wick.Runtime companion envelope, parse
            // it structurally and route by kind. Envelopes are split out from raw Godot
            // output so they do not feed the legacy GodotExceptionParser below.
            if (WickEnvelopeParser.IsEnvelope(line))
            {
                var envelope = WickEnvelopeParser.Parse(line);
                if (envelope is not null)
                {
                    switch (envelope.Kind)
                    {
                        case "handshake":
                            if (envelope.Payload is { } hp)
                            {
                                var port = WickEnvelopeParser.TryProjectHandshakePort(hp);
                                if (port is int p)
                                {
                                    _onHandshake?.Invoke(p);
                                }
                            }
                            break;
                        case "exception":
                            if (envelope.Payload is { } ep)
                            {
                                var raw = WickEnvelopeParser.TryProjectException(ep, line);
                                if (raw is not null)
                                {
                                    // Flush any partially-accumulated legacy error block first.
                                    if (inErrorBlock && errorBlock.Count > 0)
                                    {
                                        var joinedLegacy = string.Join('\n', errorBlock);
                                        var parsedLegacy = GodotExceptionParser.Parse(joinedLegacy);
                                        if (parsedLegacy is not null)
                                            yield return parsedLegacy;
                                        else if (errorBlock.Count > 1)
                                            yield return MakeUnparsed(errorBlock[0], joinedLegacy);
                                        errorBlock.Clear();
                                        inErrorBlock = false;
                                    }
                                    yield return raw;
                                }
                            }
                            break;
                        case "log":
                            if (envelope.Payload is { } lp)
                            {
                                var logLine = WickEnvelopeParser.TryProjectLogLine(lp);
                                if (logLine is not null)
                                {
                                    _logBuffer?.Add(logLine);
                                }
                            }
                            break;
                    }
                }
                continue;
            }

            // Feed all non-envelope output to log buffer
            _logBuffer?.Add(line);

            if (line.TrimStart().StartsWith("ERROR:", StringComparison.Ordinal))
            {
                // Flush any previous error block
                if (inErrorBlock && errorBlock.Count > 0)
                {
                    var joined = string.Join('\n', errorBlock);
                    var parsed = GodotExceptionParser.Parse(joined);
                    if (parsed is not null)
                        yield return parsed;
                    else if (errorBlock.Count > 1)
                        yield return MakeUnparsed(errorBlock[0], joined);
                    errorBlock.Clear();
                }

                inErrorBlock = true;
                errorBlock.Add(line);
            }
            else if (inErrorBlock)
            {
                var trimmed = line.TrimStart();
                // Stack trace continuation: lines starting with "at " or "at:" or
                // "C# backtrace" or "---" or "--->" or "[" or whitespace-only
                if (trimmed.StartsWith("at ", StringComparison.Ordinal)
                    || trimmed.StartsWith("at:", StringComparison.Ordinal)
                    || trimmed.StartsWith("C# backtrace", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith('[')
                    || trimmed.StartsWith("---", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(line))
                {
                    errorBlock.Add(line);
                }
                else
                {
                    // End of error block — flush
                    var joined = string.Join('\n', errorBlock);
                    var parsed = GodotExceptionParser.Parse(joined);
                    if (parsed is not null)
                        yield return parsed;
                    else if (errorBlock.Count > 1)
                        yield return MakeUnparsed(errorBlock[0], joined);
                    errorBlock.Clear();
                    inErrorBlock = false;
                }
            }
        }

        // Flush any remaining error block
        if (inErrorBlock && errorBlock.Count > 0)
        {
            var joined = string.Join('\n', errorBlock);
            var parsed = GodotExceptionParser.Parse(joined);
            if (parsed is not null)
                yield return parsed;
            else if (errorBlock.Count > 1)
                yield return MakeUnparsed(errorBlock[0], joined);
        }
    }

    /// <summary>
    /// Surfaces an error block that looks like an exception but didn't match
    /// the parser. Keeps the raw text so developers can still see the failure
    /// when Godot's stderr format drifts.
    /// </summary>
    private static RawException MakeUnparsed(string firstLine, string rawText)
    {
        const int messageMax = 200;
        var msg = firstLine.Length > messageMax ? firstLine[..messageMax] : firstLine;
        return new RawException
        {
            Type = "Wick.Unparsed",
            Message = msg,
            RawText = rawText,
            Frames = [],
        };
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        System.IO.StreamReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (line is null)
                yield break; // Process exited

            yield return line;
        }
    }
}
