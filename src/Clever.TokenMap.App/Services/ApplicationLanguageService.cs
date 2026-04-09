using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Clever.TokenMap.Core.Settings;

namespace Clever.TokenMap.App.Services;

public sealed class ApplicationLanguageService : IApplicationLanguageService
{
    private readonly CultureInfo _systemCulture;
    private readonly IReadOnlyList<CultureInfo> _supportedCultures;

    public ApplicationLanguageService()
        : this(
            CultureInfo.InstalledUICulture,
            DiscoverSupportedCultures(typeof(ApplicationLanguageService).Assembly))
    {
    }

    internal ApplicationLanguageService(
        CultureInfo systemCulture,
        IReadOnlyList<CultureInfo> supportedCultures)
    {
        _systemCulture = systemCulture ?? throw new ArgumentNullException(nameof(systemCulture));
        _supportedCultures = supportedCultures?.Count > 0
            ? supportedCultures
            : [CultureInfo.GetCultureInfo(ApplicationLanguageTags.Default)];
        SupportedCultures = _supportedCultures;
        CurrentPreferenceTag = ApplicationLanguageTags.System;
        EffectiveCulture = ResolveCulture(CurrentPreferenceTag);
    }

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<CultureInfo> SupportedCultures { get; }

    public string CurrentPreferenceTag { get; private set; }

    public CultureInfo EffectiveCulture { get; private set; }

    public string NormalizePreferenceTag(string? preferenceTag)
    {
        var normalizedTag = ApplicationLanguageTags.Normalize(preferenceTag);
        if (ApplicationLanguageTags.IsSystem(normalizedTag))
        {
            return ApplicationLanguageTags.System;
        }

        return TryResolveSupportedCulture(normalizedTag, out var culture)
            ? culture.Name
            : ApplicationLanguageTags.System;
    }

    public string NormalizeSupportedCultureTag(string? languageTag, string? fallbackLanguageTag = null)
    {
        if (TryResolveSupportedCulture(languageTag, out var culture))
        {
            return culture.Name;
        }

        if (TryResolveSupportedCulture(fallbackLanguageTag, out culture))
        {
            return culture.Name;
        }

        return _supportedCultures[0].Name;
    }

    public void ApplyPreference(string? preferenceTag)
    {
        var normalizedPreferenceTag = NormalizePreferenceTag(preferenceTag);
        var resolvedCulture = ResolveCulture(normalizedPreferenceTag);
        var changed = !string.Equals(normalizedPreferenceTag, CurrentPreferenceTag, StringComparison.OrdinalIgnoreCase) ||
                      !string.Equals(resolvedCulture.Name, EffectiveCulture.Name, StringComparison.OrdinalIgnoreCase);

        CurrentPreferenceTag = normalizedPreferenceTag;
        EffectiveCulture = resolvedCulture;
        CultureInfo.CurrentCulture = resolvedCulture;
        CultureInfo.CurrentUICulture = resolvedCulture;

        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private CultureInfo ResolveCulture(string preferenceTag)
    {
        if (!ApplicationLanguageTags.IsSystem(preferenceTag) &&
            TryResolveSupportedCulture(preferenceTag, out var exactMatch))
        {
            return exactMatch;
        }

        return ResolveSupportedSystemCulture();
    }

    private CultureInfo ResolveSupportedSystemCulture()
    {
        if (_supportedCultures.FirstOrDefault(culture =>
                string.Equals(culture.Name, _systemCulture.Name, StringComparison.OrdinalIgnoreCase)) is { } exactMatch)
        {
            return exactMatch;
        }

        if (_supportedCultures.FirstOrDefault(culture =>
                string.Equals(culture.TwoLetterISOLanguageName, _systemCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)) is { } languageMatch)
        {
            return languageMatch;
        }

        return _supportedCultures.FirstOrDefault(culture =>
                   string.Equals(culture.Name, ApplicationLanguageTags.Default, StringComparison.OrdinalIgnoreCase))
               ?? _supportedCultures[0];
    }

    private bool TryResolveSupportedCulture(string? languageTag, out CultureInfo culture)
    {
        culture = _supportedCultures[0];
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return false;
        }

        CultureInfo requestedCulture;
        try
        {
            requestedCulture = CultureInfo.GetCultureInfo(languageTag.Trim());
        }
        catch (CultureNotFoundException)
        {
            return false;
        }

        if (_supportedCultures.FirstOrDefault(supportedCulture =>
                string.Equals(supportedCulture.Name, requestedCulture.Name, StringComparison.OrdinalIgnoreCase)) is { } exactMatch)
        {
            culture = exactMatch;
            return true;
        }

        if (_supportedCultures.FirstOrDefault(supportedCulture =>
                string.Equals(supportedCulture.TwoLetterISOLanguageName, requestedCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)) is { } languageMatch)
        {
            culture = languageMatch;
            return true;
        }

        return false;
    }

    internal static IReadOnlyList<CultureInfo> DiscoverSupportedCultures(Assembly resourceAssembly)
    {
        ArgumentNullException.ThrowIfNull(resourceAssembly);

        var cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationLanguageTags.Default] = CultureInfo.GetCultureInfo(ApplicationLanguageTags.Default),
        };

        var satelliteAssemblyFileName = $"{resourceAssembly.GetName().Name}.resources.dll";
        foreach (var directoryPath in Directory.EnumerateDirectories(AppContext.BaseDirectory))
        {
            var cultureName = Path.GetFileName(directoryPath);
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                continue;
            }

            var satelliteAssemblyPath = Path.Combine(directoryPath, satelliteAssemblyFileName);
            if (!File.Exists(satelliteAssemblyPath))
            {
                continue;
            }

            try
            {
                var culture = CultureInfo.GetCultureInfo(cultureName);
                cultures[culture.Name] = culture;
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return cultures.Values
            .OrderBy(static culture => culture.Name.Equals(ApplicationLanguageTags.Default, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(static culture => culture.NativeName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
