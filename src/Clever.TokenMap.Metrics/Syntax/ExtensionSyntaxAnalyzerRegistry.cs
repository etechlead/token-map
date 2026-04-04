using System.IO;

namespace Clever.TokenMap.Metrics.Syntax;

public sealed class ExtensionSyntaxAnalyzerRegistry : ISyntaxAnalyzerRegistry
{
    private readonly IReadOnlyList<ISyntaxAnalyzer> _analyzers;

    public ExtensionSyntaxAnalyzerRegistry(IEnumerable<ISyntaxAnalyzer>? analyzers = null)
    {
        _analyzers = analyzers?
            .ToArray()
            ?? [];
    }

    public bool TryResolve(string fullPath, out ISyntaxAnalyzer analyzer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        var extension = Path.GetExtension(fullPath);
        foreach (var candidate in _analyzers)
        {
            if (!candidate.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!candidate.CanAnalyze(fullPath))
            {
                continue;
            }

            analyzer = candidate;
            return true;
        }

        analyzer = null!;
        return false;
    }
}
