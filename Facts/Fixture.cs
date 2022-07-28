using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using nemuikoneko.DiscriminatedUnions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Facts;

internal sealed class Fixture
{
    internal static Compilation CreateCompilation(SyntaxTree[] syntaxTrees)
        => CSharpCompilation.Create(
            "compilation",
            syntaxTrees,
            new[] { MetadataReference.CreateFromFile(typeof(Binder).GetTypeInfo().Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.ConsoleApplication));

    internal static (Compilation compilation, List<Diagnostic> diagnostics) RunGenerator(Compilation inputCompilation)
    {
        var generatorDriver = CSharpGeneratorDriver.Create(new SourceGenerator());
        generatorDriver.RunGeneratorsAndUpdateCompilation(inputCompilation, out var outputCompilation, out var diagnostics);
        return (outputCompilation, diagnostics.ToList());
    }
}
