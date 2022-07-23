using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace DiscriminatedUnions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InitializationAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor TestDiagnostic = AnalyzerHelper.BuildDiagnosticDescriptor("DU1", "Test"); // TODO Update

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(TestDiagnostic);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, ImmutableArray.Create(SyntaxKind.DefaultKeyword));
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
    }
}
