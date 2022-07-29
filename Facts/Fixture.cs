using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using nemuikoneko.DiscriminatedUnions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Facts;

internal sealed class Fixture
{
    internal static async Task SetUpEnvironmentAsync(SyntaxTree syntaxTree)
    {
        var project = await CreateProjectWithSingleDocumentAsync(syntaxTree);

        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
            throw new Exception("Failed to retrieve compilation");

        var compilationWithAnalyzer = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new Analyzer()));

        foreach (var diagnostic in await compilationWithAnalyzer.GetAllDiagnosticsAsync())
        {
            var changedDocuments = await RunCodeFixProvider(project.Documents.Single(), diagnostic);
        }
    }

    private static async Task<Project> CreateProjectWithSingleDocumentAsync(SyntaxTree syntaxTree)
        => new AdhocWorkspace()
            .CurrentSolution
            .AddProject(ProjectId.CreateNewId(), "ProjectName", "AssemblyName", LanguageNames.CSharp)
            .Projects
            .Single()
            .AddDocument("Document.cs", await syntaxTree.GetRootAsync())
            .Project
            .AddMetadataReferences(
                Basic.Reference.Assemblies.Net60.All
                    .Append(MetadataReference.CreateFromFile(typeof(SourceGenerator).Assembly.Location)))
            .AddAnalyzerReference(new AnalyzerFileReference(typeof(SourceGenerator).Assembly.Location, new AssemblyLoader()));

    private static async Task<List<Document>> RunCodeFixProvider(Document document, Diagnostic diagnostic)
    {
        var codeActions = new List<CodeAction>();
        var codeFixContext = new CodeFixContext(document, diagnostic, (a, d) => codeActions.Add(a), CancellationToken.None);

        await new DiscriminatedUnionCodeFixProvider().RegisterCodeFixesAsync(codeFixContext);

        var changedDocumentTasks = codeActions
            .Select(async codeAction => await codeAction.GetOperationsAsync(CancellationToken.None))
            .Select(async operations => (await operations).OfType<ApplyChangesOperation>().Single().ChangedSolution)
            .Select(async changedSolution => (await changedSolution).Projects.Single().Documents.Single(d => d.Id == document.Id))
            .ToList();

        var changedDocuments = new List<Document>();
        foreach (var changedDocumentTask in changedDocumentTasks)
        {
            changedDocuments.Add(await changedDocumentTask);
        }
        return changedDocuments;
    }

    private sealed class AssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath) { }
        public Assembly LoadFromPath(string fullPath) => Assembly.LoadFrom(fullPath);
    }
}
