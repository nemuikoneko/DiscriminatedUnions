using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

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

        context.RegisterSyntaxNodeAction(
            AnalyzeDefaultExpression,
            ImmutableArray.Create(
                SyntaxKind.DefaultExpression,
                SyntaxKind.DefaultLiteralExpression));
    }

    private static void AnalyzeDefaultExpression(SyntaxNodeAnalysisContext context)
    {
        var typeSymbol = context.SemanticModel.GetTypeInfo(context.Node).Type;
        if (typeSymbol == null)
            return;

        var defaultExprTypeIsUnionType = typeSymbol
            .OriginalDefinition
            .DeclaringSyntaxReferences
            .Select(syntaxReference => syntaxReference.GetSyntax())
            .Where(node => (node as StructDeclarationSyntax) != null)
            .Cast<StructDeclarationSyntax>()
            .Where(structDeclNode => structDeclNode.HasUnionAttribute())
            .Any();

        if (defaultExprTypeIsUnionType)
            context.ReportDiagnostic(Diagnostic.Create(TestDiagnostic, context.Node.GetLocation()));
    }
}
