using System.Text.RegularExpressions;

namespace Wick.Providers.GDScript;

/// <summary>
/// Parses GDScript (.gd) files to extract structural information:
/// class_name, extends, functions, signals, variables, exports, etc.
/// Reimplements GDScript parsing concepts from GoPeak (MIT, © 2025 Solomon Elias / HaD0Yun).
/// </summary>
public static partial class GDScriptParser
{
    /// <summary>
    /// Parses a GDScript file and returns its structural information.
    /// </summary>
    public static GDScriptInfo Parse(string content)
    {
        var lines = content.Split('\n');

        string? className = null;
        string? extends = null;
        var functions = new List<GDFunction>();
        var signals = new List<GDSignal>();
        var variables = new List<GDVariable>();
        var constants = new List<GDConstant>();
        var enums = new List<GDEnumDeclaration>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            var trimmed = line.Trim();
            var lineNumber = i + 1;

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            // class_name
            var classMatch = ClassNameRegex().Match(trimmed);
            if (classMatch.Success)
            {
                className = classMatch.Groups[1].Value;
                continue;
            }

            // extends
            var extendsMatch = ExtendsRegex().Match(trimmed);
            if (extendsMatch.Success)
            {
                extends = extendsMatch.Groups[1].Value;
                continue;
            }

            // signal
            var signalMatch = SignalRegex().Match(trimmed);
            if (signalMatch.Success)
            {
                signals.Add(new GDSignal
                {
                    Name = signalMatch.Groups[1].Value,
                    Parameters = signalMatch.Groups[2].Value.Trim(),
                    Line = lineNumber,
                });
                continue;
            }

            // @export var
            var exportMatch = ExportVarRegex().Match(trimmed);
            if (exportMatch.Success)
            {
                variables.Add(new GDVariable
                {
                    Name = exportMatch.Groups[2].Value,
                    Type = exportMatch.Groups[3].Success ? exportMatch.Groups[3].Value : null,
                    IsExport = true,
                    ExportHint = exportMatch.Groups[1].Value,
                    Line = lineNumber,
                });
                continue;
            }

            // @onready var
            var onreadyMatch = OnreadyVarRegex().Match(trimmed);
            if (onreadyMatch.Success)
            {
                variables.Add(new GDVariable
                {
                    Name = onreadyMatch.Groups[1].Value,
                    Type = onreadyMatch.Groups[2].Success ? onreadyMatch.Groups[2].Value : null,
                    IsOnready = true,
                    Line = lineNumber,
                });
                continue;
            }

            // var
            var varMatch = VarRegex().Match(trimmed);
            if (varMatch.Success)
            {
                variables.Add(new GDVariable
                {
                    Name = varMatch.Groups[1].Value,
                    Type = varMatch.Groups[2].Success ? varMatch.Groups[2].Value : null,
                    Line = lineNumber,
                });
                continue;
            }

            // const
            var constMatch = ConstRegex().Match(trimmed);
            if (constMatch.Success)
            {
                constants.Add(new GDConstant
                {
                    Name = constMatch.Groups[1].Value,
                    Value = constMatch.Groups[2].Value,
                    Line = lineNumber,
                });
                continue;
            }

            // enum
            var enumMatch = EnumRegex().Match(trimmed);
            if (enumMatch.Success)
            {
                enums.Add(new GDEnumDeclaration
                {
                    Name = enumMatch.Groups[1].Value,
                    Line = lineNumber,
                });
                continue;
            }

            // func
            var funcMatch = FuncRegex().Match(trimmed);
            if (funcMatch.Success)
            {
                var funcName = funcMatch.Groups[1].Value;
                functions.Add(new GDFunction
                {
                    Name = funcName,
                    Parameters = funcMatch.Groups[2].Value.Trim(),
                    ReturnType = funcMatch.Groups[3].Success ? funcMatch.Groups[3].Value : null,
                    IsPrivate = funcName.StartsWith('_'),
                    IsOverride = funcName is "_ready" or "_process" or "_physics_process" or "_input"
                        or "_unhandled_input" or "_enter_tree" or "_exit_tree" or "_init",
                    Line = lineNumber,
                });
            }
        }

        return new GDScriptInfo
        {
            ClassName = className,
            Extends = extends,
            Functions = functions,
            Signals = signals,
            Variables = variables,
            Constants = constants,
            Enums = enums,
        };
    }

    [GeneratedRegex(@"^class_name\s+(\w+)")]
    private static partial Regex ClassNameRegex();

    [GeneratedRegex(@"^extends\s+(.+)$")]
    private static partial Regex ExtendsRegex();

    [GeneratedRegex(@"^signal\s+(\w+)\s*\(?(.*?)\)?$")]
    private static partial Regex SignalRegex();

    [GeneratedRegex(@"^@export(?:\(([^)]*)\))?\s+var\s+(\w+)\s*(?::\s*(\w+))?")]
    private static partial Regex ExportVarRegex();

    [GeneratedRegex(@"^@onready\s+var\s+(\w+)\s*(?::\s*(\w+))?")]
    private static partial Regex OnreadyVarRegex();

    [GeneratedRegex(@"^var\s+(\w+)\s*(?::\s*(\w+))?")]
    private static partial Regex VarRegex();

    [GeneratedRegex(@"^const\s+(\w+)\s*=\s*(.+)$")]
    private static partial Regex ConstRegex();

    [GeneratedRegex(@"^enum\s+(\w+)")]
    private static partial Regex EnumRegex();

    [GeneratedRegex(@"^func\s+(\w+)\s*\((.*?)\)\s*(?:->\s*(\w+))?")]
    private static partial Regex FuncRegex();
}

public sealed class GDScriptInfo
{
    public string? ClassName { get; init; }
    public string? Extends { get; init; }
    public IReadOnlyList<GDFunction> Functions { get; init; } = [];
    public IReadOnlyList<GDSignal> Signals { get; init; } = [];
    public IReadOnlyList<GDVariable> Variables { get; init; } = [];
    public IReadOnlyList<GDConstant> Constants { get; init; } = [];
    public IReadOnlyList<GDEnumDeclaration> Enums { get; init; } = [];
}

public sealed class GDFunction
{
    public required string Name { get; init; }
    public string? Parameters { get; init; }
    public string? ReturnType { get; init; }
    public bool IsPrivate { get; init; }
    public bool IsOverride { get; init; }
    public int Line { get; init; }
}

public sealed class GDSignal
{
    public required string Name { get; init; }
    public string? Parameters { get; init; }
    public int Line { get; init; }
}

public sealed class GDVariable
{
    public required string Name { get; init; }
    public string? Type { get; init; }
    public bool IsExport { get; init; }
    public bool IsOnready { get; init; }
    public string? ExportHint { get; init; }
    public int Line { get; init; }
}

public sealed class GDConstant
{
    public required string Name { get; init; }
    public required string Value { get; init; }
    public int Line { get; init; }
}

public sealed class GDEnumDeclaration
{
    public required string Name { get; init; }
    public int Line { get; init; }
}
