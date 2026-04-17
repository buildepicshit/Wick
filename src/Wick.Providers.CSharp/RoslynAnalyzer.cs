using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wick.Providers.CSharp;

/// <summary>
/// Uses Roslyn to analyze C# source files and extract structural information:
/// classes, methods, properties, fields, attributes, inheritance, etc.
/// </summary>
public static class RoslynAnalyzer
{
    /// <summary>
    /// Analyzes a C# source file and returns its structural information.
    /// </summary>
    public static CSharpFileInfo Analyze(string content, string? filePath = null)
    {
        var tree = CSharpSyntaxTree.ParseText(content, path: filePath ?? "");
        var root = tree.GetCompilationUnitRoot();

        var usings = new List<string>();
        foreach (var u in root.Usings)
        {
            usings.Add(u.Name?.ToString() ?? "");
        }

        string? ns = null;
        var nsDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsDecl is not null)
        {
            ns = nsDecl.Name.ToString();
        }

        var types = new List<CSharpTypeInfo>();
        foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            types.Add(AnalyzeType(typeDecl));
        }
        foreach (var enumDecl in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
        {
            types.Add(new CSharpTypeInfo
            {
                Name = enumDecl.Identifier.Text,
                Kind = "enum",
                Modifiers = enumDecl.Modifiers.ToString(),
                Line = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Members = enumDecl.Members.Select(m => m.Identifier.Text).ToList(),
            });
        }

        return new CSharpFileInfo
        {
            FilePath = filePath,
            Namespace = ns,
            Usings = usings,
            Types = types,
        };
    }

    private static CSharpTypeInfo AnalyzeType(TypeDeclarationSyntax typeDecl)
    {
        var baseTypes = typeDecl.BaseList is not null
            ? typeDecl.BaseList.Types.Select(t => t.Type.ToString()).ToList()
            : new List<string>();

        var attributes = new List<string>();
        foreach (var attrList in typeDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                attributes.Add(attr.Name.ToString());
            }
        }

        var methods = new List<CSharpMethodInfo>();
        foreach (var method in typeDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            methods.Add(new CSharpMethodInfo
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                Parameters = method.ParameterList.Parameters.Select(p =>
                    $"{p.Type} {p.Identifier}").ToList(),
                Modifiers = method.Modifiers.ToString(),
                Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Attributes = method.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Select(a => a.Name.ToString())
                    .ToList(),
            });
        }

        var properties = new List<CSharpPropertyInfo>();
        foreach (var prop in typeDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            properties.Add(new CSharpPropertyInfo
            {
                Name = prop.Identifier.Text,
                Type = prop.Type.ToString(),
                Modifiers = prop.Modifiers.ToString(),
                Line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                Attributes = prop.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Select(a => a.Name.ToString())
                    .ToList(),
            });
        }

        var fields = new List<CSharpFieldInfo>();
        foreach (var field in typeDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                fields.Add(new CSharpFieldInfo
                {
                    Name = variable.Identifier.Text,
                    Type = field.Declaration.Type.ToString(),
                    Modifiers = field.Modifiers.ToString(),
                    Line = field.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                });
            }
        }

        return new CSharpTypeInfo
        {
            Name = typeDecl.Identifier.Text,
            Kind = typeDecl switch
            {
                ClassDeclarationSyntax => "class",
                StructDeclarationSyntax => "struct",
                InterfaceDeclarationSyntax => "interface",
                RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
                _ => "type",
            },
            Modifiers = typeDecl.Modifiers.ToString(),
            Line = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            BaseTypes = baseTypes,
            Attributes = attributes,
            Methods = methods,
            Properties = properties,
            Fields = fields,
        };
    }
}

public sealed class CSharpFileInfo
{
    public string? FilePath { get; init; }
    public string? Namespace { get; init; }
    public IReadOnlyList<string> Usings { get; init; } = [];
    public IReadOnlyList<CSharpTypeInfo> Types { get; init; } = [];
}

public sealed class CSharpTypeInfo
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public IReadOnlyList<string> BaseTypes { get; init; } = [];
    public IReadOnlyList<string> Attributes { get; init; } = [];
    public IReadOnlyList<CSharpMethodInfo> Methods { get; init; } = [];
    public IReadOnlyList<CSharpPropertyInfo> Properties { get; init; } = [];
    public IReadOnlyList<CSharpFieldInfo> Fields { get; init; } = [];
    public IReadOnlyList<string> Members { get; init; } = [];
}

public sealed class CSharpMethodInfo
{
    public required string Name { get; init; }
    public required string ReturnType { get; init; }
    public IReadOnlyList<string> Parameters { get; init; } = [];
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public IReadOnlyList<string> Attributes { get; init; } = [];
}

public sealed class CSharpPropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
    public IReadOnlyList<string> Attributes { get; init; } = [];
}

public sealed class CSharpFieldInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? Modifiers { get; init; }
    public int Line { get; init; }
}
