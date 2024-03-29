﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace nemuikoneko.DiscriminatedUnions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Analyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor DefaultInitializationNotAllowed = AnalyzerHelper.BuildDiagnosticDescriptor(
        "DU1",
        "Discriminated union types are not allowed to be initialized by a default expression or by a constructor");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DefaultInitializationNotAllowed);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        context.RegisterSyntaxNodeAction(
            AnalyzeInitializationExpression,
            ImmutableArray.Create(
                SyntaxKind.DefaultExpression,
                SyntaxKind.DefaultLiteralExpression,
                SyntaxKind.ObjectCreationExpression,
                SyntaxKind.ImplicitObjectCreationExpression));
    }

    private static bool NodeIsPartOfGeneratedCode(SyntaxNode node) => node.SyntaxTree.FilePath.Contains(".g.cs");

    private static void AnalyzeInitializationExpression(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        if (NodeIsPartOfGeneratedCode(node))
            return;

        var typeSymbol = context.SemanticModel.GetTypeInfo(node).Type;
        if (typeSymbol == null)
            return;

        var unionAttr = typeSymbol
            .OriginalDefinition
            .DeclaringSyntaxReferences
            .Select(syntaxReference => syntaxReference.GetSyntax())
            .Where(node => (node as StructDeclarationSyntax) != null)
            .Cast<StructDeclarationSyntax>()
            .Select(structDeclNode => structDeclNode.GetUnionAttribute(context.SemanticModel))
            .Where(unionAttr => unionAttr != null)
            .SingleOrDefault();

        if (unionAttr == null)
            return;

        if (!unionAttr.AllowDefault)
        {
            context.ReportDiagnostic(Diagnostic.Create(DefaultInitializationNotAllowed, node.GetLocation()));
        }
    }
}
