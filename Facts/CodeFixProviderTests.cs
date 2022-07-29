using Microsoft.CodeAnalysis.CSharp;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Facts;

public sealed class CodeFixProviderTests
{
    [Fact]
    public async Task DebugAsync()
    {
        var fileContents = File.ReadAllText("../../../../TestProject/Program.cs");
        await Fixture.SetUpEnvironmentAsync(CSharpSyntaxTree.ParseText(fileContents));
    }
}