using System;
using System.Collections.Generic;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Settings;
using System.Globalization;
using System.Linq;

namespace Clever.TokenMap.Tests.Headless.AppState;

public sealed class LocalizationStateTests
{
    [Fact]
    public void UsesRussianResources_WhenPreferenceIsRussian()
    {
        var languageService = new ApplicationLanguageService();
        var localization = new LocalizationState(languageService);

        languageService.ApplyPreference("ru");

        Assert.Equal("\u0414\u0435\u0440\u0435\u0432\u043e \u043f\u0440\u043e\u0435\u043a\u0442\u0430", localization.ProjectTree);
        Assert.Equal("\u041e\u0442\u043a\u0440\u044b\u0442\u044c", localization.OpenAction);
        Assert.Equal("\u0421\u0442\u0440\u0443\u043a\u0442\u0443\u0440\u043d\u0430\u044f \u0431\u0430\u0437\u0430", localization.ExplainabilityStructuralBaseTitle);
        Assert.Equal("\u0422\u043e\u043a\u0435\u043d\u044b", localization.GetMetricDisplayName("tokens", "fallback"));
        Assert.Equal("\u0420\u0435\u0444\u0430\u043a\u0442\u043e\u0440", localization.GetMetricShortName("refactor_priority_points", "fallback"));
        Assert.Equal("\u0417\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u0435 \u0430\u043d\u0430\u043b\u0438\u0437, \u0447\u0442\u043e\u0431\u044b \u0437\u0430\u043f\u043e\u043b\u043d\u0438\u0442\u044c treemap.", localization.GetTreemapPlaceholderNoSnapshot());
        Assert.Equal(
            "\u041f\u043e\u043a\u0430\u0437\u0430\u0442\u044c \u0432 \u041f\u0440\u043e\u0432\u043e\u0434\u043d\u0438\u043a\u0435",
            localization.GetRevealMenuHeader("Reveal in Explorer"));
        Assert.Equal(
            "\u041e\u0434\u0438\u043d \u0440\u0430\u0437 \u043f\u0440\u043e\u0430\u043d\u0430\u043b\u0438\u0437\u0438\u0440\u0443\u0439\u0442\u0435 \u043f\u0430\u043f\u043a\u0443, \u0438 \u043e\u043d\u0430 \u043f\u043e\u044f\u0432\u0438\u0442\u0441\u044f \u0437\u0434\u0435\u0441\u044c.",
            localization.RecentFoldersEmptyFlyoutSecondaryText);
        Assert.Equal(
            "TokenMap \u043d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u0437\u0430\u043f\u0443\u0441\u0442\u0438\u0442\u044c.",
            localization.StartupFailedToStart);
    }

    [Fact]
    public void ApplicationLanguageOptions_ExposeAllSupportedCulturesFromLanguageService()
    {
        var languageService = new StubApplicationLanguageService(
            supportedCultures:
            [
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.GetCultureInfo("ru"),
                CultureInfo.GetCultureInfo("fr"),
            ]);
        var localization = new LocalizationState(languageService);

        Assert.Collection(
            localization.ApplicationLanguageOptions,
            option => Assert.Equal(ApplicationLanguageTags.System, option.Value),
            option => Assert.Equal("en-US", option.Value),
            option => Assert.Equal("ru", option.Value),
            option => Assert.Equal("fr", option.Value));
    }

    [Fact]
    public void PromptLanguageOptions_ReuseSupportedCulturesWithoutSystemEntry()
    {
        var languageService = new StubApplicationLanguageService(
            supportedCultures:
            [
                CultureInfo.GetCultureInfo("en-US"),
                CultureInfo.GetCultureInfo("ru"),
                CultureInfo.GetCultureInfo("fr"),
            ]);
        var localization = new LocalizationState(languageService);

        Assert.Collection(
            localization.PromptLanguageOptions,
            option => Assert.Equal("en-US", option.Value),
            option => Assert.Equal("ru", option.Value),
            option => Assert.Equal("fr", option.Value));
    }

    private sealed class StubApplicationLanguageService : IApplicationLanguageService
    {
        private readonly IReadOnlyList<CultureInfo> _supportedCultures;

        public StubApplicationLanguageService(IReadOnlyList<CultureInfo> supportedCultures)
        {
            _supportedCultures = supportedCultures;
            EffectiveCulture = _supportedCultures[0];
        }

        public event EventHandler? LanguageChanged;

        public IReadOnlyList<CultureInfo> SupportedCultures => _supportedCultures;

        public string CurrentPreferenceTag { get; private set; } = ApplicationLanguageTags.System;

        public CultureInfo EffectiveCulture { get; private set; }

        public string NormalizePreferenceTag(string? preferenceTag) => ApplicationLanguageTags.Normalize(preferenceTag);

        public string NormalizeSupportedCultureTag(string? languageTag, string? fallbackLanguageTag = null) =>
            AppSettingsCanonicalizer.NormalizePromptLanguageTag(languageTag)
            ?? AppSettingsCanonicalizer.NormalizePromptLanguageTag(fallbackLanguageTag)
            ?? _supportedCultures[0].Name;

        public void ApplyPreference(string? preferenceTag)
        {
            CurrentPreferenceTag = NormalizePreferenceTag(preferenceTag);
            EffectiveCulture = _supportedCultures.FirstOrDefault(culture =>
                                   string.Equals(culture.Name, CurrentPreferenceTag, StringComparison.OrdinalIgnoreCase))
                               ?? _supportedCultures[0];
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
