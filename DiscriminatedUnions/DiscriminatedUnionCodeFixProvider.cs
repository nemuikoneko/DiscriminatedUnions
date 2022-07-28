using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace nemuikoneko.DiscriminatedUnions;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class DiscriminatedUnionCodeFixProvider : CodeFixProvider
{
    private const string InvalidMethodArgDiagnosticId = "CS7036";

    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(InvalidMethodArgDiagnosticId);

    public sealed override FixAllProvider? GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;


    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics.First();
        var document = context.Document;
        var nodeToFixSpan = context.Span;

        if (diagnostic.Id != InvalidMethodArgDiagnosticId)
            return;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            throw new Exception("Failed to retrieve semantic model");

        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (syntaxRoot == null)
            throw new Exception("Failed to retrieve syntax root");

        var nodeToFix = GetNodeToFix(syntaxRoot, nodeToFixSpan);
        if (nodeToFix == null)
            throw new Exception("Node to fix not found");

        var methodSymbol = GetMethodSymbol(nodeToFix, semanticModel);
        if (!IsMethodPartOfGeneratedUnion(methodSymbol, semanticModel))
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: diagnostic.Id,
                createChangedDocument: cancellationToken => FixDiagnostic(document, nodeToFix, methodSymbol),
                equivalenceKey: diagnostic.Id),
            diagnostic);
    }

    private static InvocationExpressionSyntax GetNodeToFix(SyntaxNode syntaxRoot, TextSpan span)
        => syntaxRoot
            .FindNode(span)
            .Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Where(node => (node.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText == CodeGenerator.MatchMethodName)
            .LastOrDefault();

    private static IMethodSymbol GetMethodSymbol(SyntaxNode node, SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetTypeInfo(node).Type?.ContainingSymbol;
        if (symbol == null)
            throw new Exception("Failed to retrieve symbol");
        return (IMethodSymbol)symbol.OriginalDefinition;
    }

    private static bool IsMethodPartOfGeneratedUnion(IMethodSymbol methodSymbol, SemanticModel semanticModel)
        => methodSymbol
            .ContainingType
            .DeclaringSyntaxReferences
            .Select(syntaxRef => syntaxRef.GetSyntax())
            .OfType<StructDeclarationSyntax>()
            .Where(structDeclNode => structDeclNode.GetUnionAttribute(semanticModel) != null)
            .Any();

    private static async Task<Document> FixDiagnostic(
        Document document,
        InvocationExpressionSyntax nodeToFix,
        IMethodSymbol methodSymbol)
    {
        await Task.Yield();
        return document;
    }
}