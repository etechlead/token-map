using Clever.TokenMap.Core.Analysis.Syntax;

namespace Clever.TokenMap.Metrics.Syntax;

public interface ISyntaxAnalyzer
{
    string LanguageId { get; }

    IReadOnlyCollection<string> FileExtensions { get; }

    bool CanAnalyze(string fullPath);

    ValueTask<SyntaxSummaryArtifact> AnalyzeAsync(
        string fullPath,
        string sourceText,
        CancellationToken cancellationToken);
}
