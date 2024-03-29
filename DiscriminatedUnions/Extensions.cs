﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace nemuikoneko.DiscriminatedUnions
{
    internal static class Extensions
    {
        internal static void ForEach<T>(this IReadOnlyList<T> list, Action<T> action)
        {
            foreach (var element in list)
            {
                action(element);
            }
        }

        internal static string Join(this IEnumerable<string> enumerable, string separator)
            => string.Join(separator, enumerable);

        internal static bool? IsPartial(this ITypeSymbol typeSymbol)
        {
            var syntaxRefs = typeSymbol.DeclaringSyntaxReferences;

            return syntaxRefs.Length switch
            {
                0 => null,
                1 => ((TypeDeclarationSyntax)syntaxRefs[0].GetSyntax()).Modifiers.Any(SyntaxKind.PartialKeyword),
                (> 1) => true,
                _ => throw new NotImplementedException()
            };
        }

        internal static bool IsPartial(this StructDeclarationSyntax structDeclNode)
            => structDeclNode.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword));

        private static bool? GetAllowDefaultAttributeArgument(AttributeSyntax attrNode, SemanticModel semanticModel)
        {
            var allowDefaultAttrArgNode = attrNode
                .DescendantNodes()
                .OfType<AttributeArgumentSyntax>()
                .Where(attrArg => attrArg.NameEquals?.Name.Identifier.ValueText == "AllowDefault")
                .SingleOrDefault();

            if (allowDefaultAttrArgNode == null)
                return null;

            return semanticModel.GetConstantValue(allowDefaultAttrArgNode.Expression).Value as bool?;
        }

        internal static UnionAttribute? GetUnionAttribute(this StructDeclarationSyntax structDeclNode, SemanticModel semanticModel)
            => structDeclNode
                .AttributeLists
                .SelectMany(attrListNode => attrListNode.Attributes)
                .Where(attrNode => (attrNode.Name as IdentifierNameSyntax)?.Identifier.ValueText == SourceGenerator.UnionAttributeName)
                .Select(attrNode =>
                {
                    var allowDefaultAttrArg = GetAllowDefaultAttributeArgument(attrNode, semanticModel);

                    return new UnionAttribute
                    {
                        AllowDefault = allowDefaultAttrArg ?? default
                    };
                }).SingleOrDefault();
    }
}
