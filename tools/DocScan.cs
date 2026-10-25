#:package Microsoft.CodeAnalysis.CSharp@4.14.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Ищет объявления без XML-комментария. Запуск: dotnet run DocScan.cs -- <корень репозитория> [--params]
var root = args[0];
var checkParams = args.Contains("--params");
var skip = new[] { @"\obj\", @"\bin\", @"\.venv\", @"\data\", @"\publish\", @"\.vs\" };
var missing = 0;
var total = 0;

foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
             .Where(p => !skip.Any(s => p.Contains(s, StringComparison.OrdinalIgnoreCase)) && !p.EndsWith(".g.cs") && !p.EndsWith(".g.i.cs"))
             .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
    var rootNode = tree.GetRoot();
    var report = new List<string>();

    foreach (var node in rootNode.DescendantNodes())
    {
        string? kind = node switch
        {
            BaseTypeDeclarationSyntax => "type",
            DelegateDeclarationSyntax => "delegate",
            MethodDeclarationSyntax => "method",
            ConstructorDeclarationSyntax => "ctor",
            PropertyDeclarationSyntax => "property",
            IndexerDeclarationSyntax => "indexer",
            FieldDeclarationSyntax => "field",
            EventDeclarationSyntax or EventFieldDeclarationSyntax => "event",
            OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => "operator",
            EnumMemberDeclarationSyntax => "enum member",
            _ => null,
        };
        if (kind is null)
            continue;

        total++;
        var doc = node.GetLeadingTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
        var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var name = node switch
        {
            BaseTypeDeclarationSyntax t => t.Identifier.Text,
            MethodDeclarationSyntax m => m.Identifier.Text,
            ConstructorDeclarationSyntax c => c.Identifier.Text,
            PropertyDeclarationSyntax p => p.Identifier.Text,
            FieldDeclarationSyntax f => string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.Text)),
            EnumMemberDeclarationSyntax e => e.Identifier.Text,
            DelegateDeclarationSyntax d => d.Identifier.Text,
            _ => node.Kind().ToString(),
        };

        if (doc == default)
        {
            report.Add($"  {line,4} {kind,-11} {name}");
            missing++;
            continue;
        }

        if (!checkParams)
            continue;

        var text = doc.ToFullString();
        var parameters = node switch
        {
            MethodDeclarationSyntax m => m.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray(),
            ConstructorDeclarationSyntax c => c.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray(),
            RecordDeclarationSyntax r => r.ParameterList?.Parameters.Select(p => p.Identifier.ValueText).ToArray() ?? [],
            _ => [],
        };
        var generatedRegex = node is MethodDeclarationSyntax gm && gm.AttributeLists.Any(list => list.Attributes.Any(a => a.Name.ToString().StartsWith("GeneratedRegex")));
        var lacking = parameters.Where(p => !text.Contains($"name=\"{p}\"")).ToArray();
        var returnsValue = !generatedRegex && node is MethodDeclarationSyntax { ReturnType: var rt } && rt.ToString() is not ("void" or "Task") && !text.Contains("<returns>") && !text.Contains("<inheritdoc");
        if (lacking.Length > 0 || returnsValue)
            report.Add($"  {line,4} {kind,-11} {name}  [{(lacking.Length > 0 ? "param: " + string.Join(",", lacking) : "")}{(returnsValue ? " returns" : "")}]");
    }

    if (report.Count > 0)
    {
        Console.WriteLine(Path.GetRelativePath(root, path));
        report.ForEach(Console.WriteLine);
    }
}

Console.WriteLine($"--- без комментария: {missing} из {total} объявлений");
