using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using nemuikoneko.DiscriminatedUnions;
using Xunit;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Threading.Tasks;

namespace Facts;

public class GeneratorTests
{
    internal static Compilation RunGeneratorAndEnsureNoDiagnostics(Compilation inputCompilation)
    {
        var (outputCompilation, diagnostics) = Fixture.RunGenerator(inputCompilation);
        if (diagnostics.Count > 0)
            throw new Exception("Generator execution resulted in diagnostics!");
        return outputCompilation;
    }

    [Fact]
    public async Task DebugAsync()
    {
        var filePath = "../../../../TestProject/Program.cs";
        var compilation = Fixture.CreateCompilation(new[] { CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)) });

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