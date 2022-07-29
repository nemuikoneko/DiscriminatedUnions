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

        var assessmentResult = await AssessDiagnostic(diagnostic, document, nodeToFixSpan);
        if (!assessmentResult.HasValue)
            return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Implement missing union cases",
                createChangedDocument: cancellationToken => FixDiagnostic(
                    document,
                    assessmentResult.Value.syntaxRoot,
                    assessmentResult.Value.nodeToFix,
                    assessmentResult.Value.method),
                equivalenceKey: diagnostic.Id),
            diagnostic);
    }

    private static async Task<(InvocationExpressionSyntax nodeToFix, IMethodSymbol method, SyntaxNode syntaxRoot)?> AssessDiagnostic(
        Diagnostic diagnostic,
        Document document,
        TextSpan nodeToFixSpan)
    {
        if (diagnostic.Id != InvalidMethodArgDiagnosticId)
            return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null)
            throw new Exception("Failed to retrieve semantic model");

        var syntaxRoot = await document.GetSyntaxRootAsync();
        if (syntaxRoot == null)
            throw new Exception("Failed to retrieve syntax root");

        var candidateNodeToFix = GetNodeToFixCandidate(syntaxRoot, nodeToFixSpan);
        if (candidateNodeToFix == null)
            return null;

        var method = GetMethodSymbol(candidateNodeToFix, semanticModel);
        if (method == null)
            return null;

        if (!IsMethodPartOfGeneratedUnion(method, semanticModel))
            return null;

        return (candidateNodeToFix, method, syntaxRoot);
    }

    private static InvocationExpressionSyntax GetNodeToFixCandidate(SyntaxNode syntaxRoot, TextSpan span)
        => syntaxRoot
            .FindNode(span)
            .Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Where(node => (node.Expression as MemberAccessExpressionSyntax)?.Name.Identifier.ValueText == CodeGenerator.MatchMethodName)
            .LastOrDefault();

    private static IMethodSymbol? GetMethodSymbol(InvocationExpressionSyntax node, SemanticModel semanticModel)
    {
        var typeSymbol = semanticModel.GetTypeInfo(node).Type;
        if (typeSymbol == null)
            throw new Exception("Failed to retrieve type symbol");
        return (typeSymbol as ITypeParameterSymbol)?.DeclaringMethod;
    }

    private static bool IsMethodPartOfGeneratedUnion(IMethodSymbol methodSymbol, SemanticModel semanticModel)
        => methodSymbol
            .ContainingType
            .DeclaringSyntaxReferences
            .Select(syntaxRef => syntaxRef.GetSyntax())
            .OfType<StructDeclarationSyntax>()
            .Where(structDeclNode => structDeclNode.GetUnionAttribute(semanticModel) != null)
            .Any();

    private static Task<Document> FixDiagnostic(
        Document document,
        SyntaxNode syntaxRoot,
        InvocationExpressionSyntax nodeToFix,
        IMethodSymbol method)
    {
        var (numUnnamedArgs, namedArgs) = GetNamedArgs(nodeToFix);
        var missingArgs = CreateMissingArguments(method, numUnnamedArgs, namedArgs, compilationUnitIncludesSystemNamespace: false);

        var newArgListNode = nodeToFix.ArgumentList.AddArguments(missingArgs.ToArray());
        var fixedNode = nodeToFix.WithArgumentList(newArgListNode);

        var newSyntaxRoot = syntaxRoot.ReplaceNode(nodeToFix, fixedNode);
        var newDocument = document.WithSyntaxRoot(newSyntaxRoot);

        return Task.FromResult(newDocument);
    }

    private static (int numUnnamedArgs, ImmutableArray<string> namedArgs) GetNamedArgs(InvocationExpressionSyntax node)
    {
        var argNames = node
            .ArgumentList
            .Arguments
            .Select(arg => arg.NameColon?.Name.Identifier.ValueText);

        var numUnnamedArgs = argNames
            .Where(argName => argName == null)
            .Count();

        var namedArgs = argNames
            .Aggregate(ImmutableArray<string>.Empty, (arr, argName) => argName == null ? arr : arr.Add(argName));

        return (numUnnamedArgs, namedArgs);
    }

    private static ImmutableArray<ITypeSymbol> GetInputTypeArgs(IParameterSymbol param)
    {
        var typeArgs = (param.Type as INamedTypeSymbol)?.TypeArguments.AsEnumerable();
        if (typeArgs == null)
            throw new Exception("Failed to retrieve type arguments");
        return typeArgs.Take(typeArgs.Count() - 1).ToImmutableArray();
    }

    private static string RenderInputTypeArgs(ImmutableArray<ITypeSymbol> typeArgs)
    {
        if (typeArgs.Length == 0)
            return "()";

        if (typeArgs.Length == 1)
            return "arg";

        var typeArgStrs = typeArgs.Select((typeArg, i) => $"arg{i + 1}");
        return $"({typeArgStrs.Join(", ")})";
    }

    private static ImmutableArray<ArgumentSyntax> CreateMissingArguments(
        IMethodSymbol method,
        int numUnnamedArgs,
        ImmutableArray<string> namedArgs,
        bool compilationUnitIncludesSystemNamespace)
    {
        var lambdaBodyStr = compilationUnitIncludesSystemNamespace
            ? "throw new NotImplementedException()"
            : "throw new System.NotImplementedException()";

        var paramsToCreateArgsFrom = method
            .Parameters
            .Skip(numUnnamedArgs)
            .Where(param => !namedArgs.Contains(param.Name));

        var argStrs = paramsToCreateArgsFrom
            .Select(param => $"{param.Name}: {RenderInputTypeArgs(GetInputTypeArgs(param))} => {lambdaBodyStr}");

        return SyntaxFactory
            .ParseArgumentList(argStrs.Join(", "))
            .Arguments
            .ToImmutableArray();
    }
}