using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using DiscriminatedUnions;
using Xunit;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Threading.Tasks;

namespace Facts;

public class GeneratorTests
{
    private static Compilation CreateCompilation(SyntaxTree[] syntaxTrees)
        => CSharpCompilation.Create("compilation",
            syntaxTrees,
            new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

    private static Compilation CreateDefaultCompilation(string mainSyntaxTreeCode, params string[] additionalSyntaxTreesCode)
    {
        static SyntaxTree CreateSyntaxTree(string code) => CSharpSyntaxTree.ParseText($"namespace App;\n{code}");

        var mainSyntaxTree = CreateSyntaxTree($@"
public class Program
{{
    public static void Main(string[] args) {{}}

    {mainSyntaxTreeCode}
}}");

        var syntaxTrees = new SyntaxTree[] { mainSyntaxTree }
            .Concat(additionalSyntaxTreesCode.Select(code => CSharpSyntaxTree.ParseText(code))).ToArray();

        return CreateCompilation(syntaxTrees);
    }

    private static (Compilation GeneratedCompilation, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(Compilation inputCompilation)
    {
        var generatorDriver = CSharpGeneratorDriver.Create(new SourceGenerator());
        generatorDriver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var generatedCompilation, out var diagnostics);
        return (generatedCompilation, diagnostics);
    }

    private static Compilation RunGeneratorAndEnsureNoDiagnostics(Compilation inputCompilation)
    {
        var (generatedCompilation, diagnostics) = RunGenerator(inputCompilation);

        if (!diagnostics.IsEmpty)
            throw new Exception("Generator execution resulted in diagnostics!");

        return generatedCompilation;
    }

    [Fact]
    public async Task DebugAsync()
    {
        var filePath = "../../../../TestProject/Program.cs";
        var compilation = CreateCompilation(new[] { CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)) });

        var generatedCompilation = RunGeneratorAndEnsureNoDiagnostics(compilation);

        var references = Basic.Reference.Assemblies.NetStandard20.All.ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Analyzer).Assembly.Location));

        var generatedCompilationWithAnalyzer = generatedCompilation
            .WithReferences(references)
            .WithAnalyzers(ImmutableArray<DiagnosticAnalyzer>.Empty.Add(new Analyzer()));

        var diagnostics = await generatedCompilationWithAnalyzer.GetAllDiagnosticsAsync();

        throw new Exception("Debugging session ended");
    }
}