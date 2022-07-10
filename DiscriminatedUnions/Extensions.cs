using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace DiscriminatedUnions
{
    internal static class Extensions
    {
        internal static T Tap<T>(this T obj, Action<T> f)
        {
            f(obj);
            return obj;
        }

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
    }
}
