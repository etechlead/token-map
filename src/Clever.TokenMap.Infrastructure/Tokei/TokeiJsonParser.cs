using System.Text.Json;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Tokei;

internal sealed class TokeiJsonParser
{
    private readonly PathNormalizer _pathNormalizer;

    public TokeiJsonParser(PathNormalizer? pathNormalizer = null)
    {
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
    }

    public IReadOnlyDictionary<string, TokeiFileStats> Parse(
        string json,
        IReadOnlyCollection<string> includedRelativePaths)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(includedRelativePaths);

        var includedPathSet = new HashSet<string>(
            includedRelativePaths.Select(_pathNormalizer.NormalizeRelativePath),
            _pathNormalizer.PathComparer);
        var result = new Dictionary<string, TokeiFileStats>(_pathNormalizer.PathComparer);

        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var languageEntry in document.RootElement.EnumerateObject())
        {
            if (languageEntry.NameEquals("Total") ||
                languageEntry.Value.ValueKind != JsonValueKind.Object ||
                !languageEntry.Value.TryGetProperty("reports", out var reportsElement) ||
                reportsElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var reportElement in reportsElement.EnumerateArray())
            {
                if (!TryParseReport(languageEntry.Name, reportElement, includedPathSet, out var relativePath, out var stats))
                {
                    continue;
                }

                result[relativePath] = stats;
            }
        }

        return result;
    }

    private bool TryParseReport(
        string language,
        JsonElement reportElement,
        IReadOnlySet<string> includedRelativePaths,
        out string relativePath,
        out TokeiFileStats stats)
    {
        relativePath = string.Empty;
        stats = null!;

        if (!reportElement.TryGetProperty("name", out var nameElement) ||
            nameElement.GetString() is not { Length: > 0 } rawName ||
            !reportElement.TryGetProperty("stats", out var statsElement) ||
            statsElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        relativePath = _pathNormalizer.NormalizeRelativePath(rawName);
        if (string.IsNullOrEmpty(relativePath) || !includedRelativePaths.Contains(relativePath))
        {
            return false;
        }

        if (!TryReadInt32(statsElement, "code", out var codeLines) ||
            !TryReadInt32(statsElement, "comments", out var commentLines) ||
            !TryReadInt32(statsElement, "blanks", out var blankLines))
        {
            return false;
        }

        stats = new TokeiFileStats
        {
            RelativePath = relativePath,
            TotalLines = codeLines + commentLines + blankLines,
            CodeLines = codeLines,
            CommentLines = commentLines,
            BlankLines = blankLines,
            Language = language,
        };

        return true;
    }

    private static bool TryReadInt32(JsonElement parent, string propertyName, out int value)
    {
        value = default;

        if (!parent.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out value))
        {
            return false;
        }

        return true;
    }
}
