using System.Globalization;

namespace Clever.TokenMap.Core.Settings;

public static class ApplicationLanguageTags
{
    public const string System = "system";
    public const string Default = "en-US";

    public static bool IsSystem(string? languageTag) =>
        string.Equals(languageTag?.Trim(), System, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag) || IsSystem(languageTag))
        {
            return System;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageTag.Trim()).Name;
        }
        catch (CultureNotFoundException)
        {
            return System;
        }
    }
}
