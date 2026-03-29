namespace Clever.TokenMap.Core.Models;

public static class GlobalExcludeList
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var normalizedEntries = new List<string>();

        foreach (var entry in entries)
        {
            var normalizedEntry = NormalizeEntry(entry);
            if (normalizedEntry is null)
            {
                continue;
            }

            normalizedEntries.Add(normalizedEntry);
        }

        return normalizedEntries;
    }

    private static string? NormalizeEntry(string? entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            return null;
        }

        var normalizedEntry = entry.Trim().Replace('\\', '/');
        while (normalizedEntry.Contains("//", StringComparison.Ordinal))
        {
            normalizedEntry = normalizedEntry.Replace("//", "/", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(normalizedEntry)
            ? null
            : normalizedEntry;
    }
}
