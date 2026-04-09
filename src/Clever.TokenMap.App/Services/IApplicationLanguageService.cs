using System;
using System.Collections.Generic;
using System.Globalization;
namespace Clever.TokenMap.App.Services;

public interface IApplicationLanguageService
{
    event EventHandler? LanguageChanged;

    IReadOnlyList<CultureInfo> SupportedCultures { get; }

    string CurrentPreferenceTag { get; }

    CultureInfo EffectiveCulture { get; }

    string NormalizePreferenceTag(string? preferenceTag);

    string NormalizeSupportedCultureTag(string? languageTag, string? fallbackLanguageTag = null);

    void ApplyPreference(string? preferenceTag);
}
