using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using Xunit;
using System.Threading.Tasks;

namespace Facts;

public class GeneratorTests
{
    [Fact]
    public async Task DebugAsync()
    {
        var filePath = "../../../../TestProject/Program.cs";

        await Fixture.SetUpEnvironmentAsync(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)));

        throw new Exception("Debugging session ended");
    }
}