using System.Globalization;

namespace Clever.TokenMap.Core.Diagnostics;

public static class AppIssueContext
{
    public static IReadOnlyDictionary<string, string> Empty { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, string> Create(params (string Key, object? Value)[] entries)
    {
        if (entries.Length == 0)
        {
            return Empty;
        }

        var context = new Dictionary<string, string>(entries.Length, StringComparer.Ordinal);
        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrWhiteSpace(key) || value is null)
            {
                continue;
            }

            context[key] = value switch
            {
                bool booleanValue => booleanValue ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }

        return context.Count == 0 ? Empty : context;
    }
}
