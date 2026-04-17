using System.Diagnostics;
using System.Text;

namespace Wick.Providers.CSharp;

/// <summary>
/// Abstraction over <c>dotnet</c> CLI invocation so build tools can be unit-tested
/// without spawning real processes. The production implementation
/// (<see cref="DotNetCli"/>.RunAsync wrapped by <see cref="DefaultDotNetCli"/>) shells out;
/// tests supply a canned <see cref="CliResult"/>.
/// </summary>
/// <remarks>
/// Arguments are passed as a list and forwarded to <see cref="ProcessStartInfo.ArgumentList"/>
/// — this avoids argument-injection holes where user-controlled paths or flags contain
/// quotes, spaces, or shell metacharacters.
/// </remarks>
public interface IDotNetCli
{
    Task<CliResult> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IDotNetCli"/> that shells out via <see cref="DotNetCli.RunAsync"/>.
/// </summary>
public sealed class DefaultDotNetCli : IDotNetCli
{
    public Task<CliResult> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
        => DotNetCli.RunAsync(arguments, workingDirectory, timeoutSeconds, cancellationToken);
}

/// <summary>
/// Wraps the dotnet CLI for build, test, clean, and package management operations.
/// </summary>
public static class DotNetCli
{
    /// <summary>
    /// Soft cap on the stdout/stderr capture buffer per stream. Build output past this
    /// size is discarded with a truncation marker — a malicious or runaway build should
    /// not OOM the MCP server.
    /// </summary>
    internal const int MaxCapturedBytes = 4 * 1024 * 1024;

    /// <summary>
    /// Runs a dotnet CLI command and returns the result.
    /// </summary>
    public static async Task<CliResult> RunAsync(IReadOnlyList<string> arguments, string workingDirectory, int timeoutSeconds = 120, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new BoundedStringBuffer(MaxCapturedBytes);
        var stderr = new BoundedStringBuffer(MaxCapturedBytes);

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return new CliResult
            {
                ExitCode = -1,
                Output = stdout.ToString(),
                Error = "Command timed out or was cancelled.",
                TimedOut = true,
            };
        }

        return new CliResult
        {
            ExitCode = process.ExitCode,
            Output = stdout.ToString(),
            Error = stderr.ToString(),
        };
    }

    /// <summary>
    /// StringBuilder-like accumulator that caps total captured bytes. Once the cap is
    /// hit, further writes are discarded and a single truncation marker is appended.
    /// </summary>
    private sealed class BoundedStringBuffer
    {
        private readonly StringBuilder _sb = new();
        private readonly int _cap;
        private bool _truncated;

        public BoundedStringBuffer(int capBytes) { _cap = capBytes; }

        public void AppendLine(string line)
        {
            if (_truncated) return;
            // +1 for the newline AppendLine will add
            if (_sb.Length + line.Length + 1 > _cap)
            {
                _sb.AppendLine($"[...output truncated — exceeded {_cap / (1024 * 1024)} MB cap]");
                _truncated = true;
                return;
            }
            _sb.AppendLine(line);
        }

        public override string ToString() => _sb.ToString();
    }
}

public sealed class CliResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public bool TimedOut { get; init; }
    public bool Success => ExitCode == 0 && !TimedOut;
}
