using Microsoft.CodeAnalysis.CodeFixes;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace nemuikoneko.DiscriminatedUnions;

internal class DiscriminatedUnionCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => throw new System.NotImplementedException();

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        throw new System.NotImplementedException();
    }
}