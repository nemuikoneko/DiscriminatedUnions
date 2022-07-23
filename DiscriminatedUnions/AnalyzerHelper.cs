using Microsoft.CodeAnalysis;

namespace DiscriminatedUnions;

internal static class AnalyzerHelper
{
    internal static DiagnosticDescriptor BuildDiagnosticDescriptor(string id, string titleAndMessage) =>
        new(id, titleAndMessage, titleAndMessage, nameof(DiscriminatedUnions), DiagnosticSeverity.Error, isEnabledByDefault: true);
}