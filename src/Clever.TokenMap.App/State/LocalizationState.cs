using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Resources;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed class LocalizationState : ObservableObject
{
    private static readonly ResourceManager ResourceManager =
        new("Clever.TokenMap.App.Resources.AppStrings", typeof(LocalizationState).Assembly);

    private static readonly string[] ObservablePropertyNames =
    [
        nameof(AppTitle),
        nameof(PreviousFoldersTitle),
        nameof(Clear),
        nameof(NoPreviousFoldersYet),
        nameof(OpenFolder),
        nameof(Missing),
        nameof(RemoveFromRecentFoldersTooltip),
        nameof(OpenLogs),
        nameof(Dismiss),
        nameof(CloseApp),
        nameof(Settings),
        nameof(CloseSettingsTooltip),
        nameof(View),
        nameof(TreemapPalette),
        nameof(TreemapPaletteWeighted),
        nameof(TreemapPaletteStudio),
        nameof(TreemapPalettePlain),
        nameof(Metrics),
        nameof(TreeOnly),
        nameof(Scan),
        nameof(RespectGitIgnore),
        nameof(UseGlobalExcludes),
        nameof(Edit),
        nameof(UseFolderExcludes),
        nameof(ScanSettingsUpdatedRescanToApply),
        nameof(RescanNow),
        nameof(Prompts),
        nameof(EditRefactorPromptTemplate),
        nameof(Theme),
        nameof(UseLightTheme),
        nameof(UseSystemTheme),
        nameof(UseDarkTheme),
        nameof(ProjectTree),
        nameof(Name),
        nameof(PercentParent),
        nameof(Treemap),
        nameof(ShowValues),
        nameof(Threshold),
        nameof(OpenAction),
        nameof(PreviewAction),
        nameof(RefactorPromptAction),
        nameof(SetAsTreemapRootAction),
        nameof(ExcludeFromScanAction),
        nameof(CopyFullPathAction),
        nameof(CopyRelativePathAction),
        nameof(OpenFolderForAnalysisTooltip),
        nameof(RunScanAgainTooltip),
        nameof(StopCurrentScanTooltip),
        nameof(Rescan),
        nameof(Stop),
        nameof(Tokens),
        nameof(Lines),
        nameof(Files),
        nameof(Share),
        nameof(PreviewAndCopyShareImageTooltip),
        nameof(OpenSettingsTooltip),
        nameof(ShareImage),
        nameof(CloseShareImagePreviewTooltip),
        nameof(ShareImagePreviewHint),
        nameof(IncludeProjectName),
        nameof(ProjectNameWatermark),
        nameof(CloseFilePreviewTooltip),
        nameof(Explainability),
        nameof(Close),
        nameof(CancelEditingExcludesTooltip),
        nameof(Cancel),
        nameof(Save),
        nameof(SaveAndRescan),
        nameof(RefactorPrompt),
        nameof(CloseRefactorPromptTooltip),
        nameof(RefactorPromptIntro),
        nameof(RefactorPromptHint),
        nameof(RefactorPromptTemplate),
        nameof(RefactorPromptTemplateScope),
        nameof(CloseRefactorPromptTemplateEditorTooltip),
        nameof(Placeholders),
        nameof(PlaceholdersHint),
        nameof(RefactorPromptTemplateEditorHint),
        nameof(ResetToDefault),
        nameof(PromptLanguage),
        nameof(ApplicationLanguage),
        nameof(SystemLanguage),
        nameof(EnglishLanguage),
        nameof(RussianLanguage),
        nameof(Copy),
        nameof(Copied),
        nameof(RevealAction),
        nameof(ShareCardTokens),
        nameof(ShareCardLines),
        nameof(ShareCardFiles),
        nameof(ShareCardMadeWith),
    ];

    private readonly IApplicationLanguageService _applicationLanguageService;

    public LocalizationState(IApplicationLanguageService applicationLanguageService)
    {
        _applicationLanguageService = applicationLanguageService ?? throw new ArgumentNullException(nameof(applicationLanguageService));
        _applicationLanguageService.LanguageChanged += ApplicationLanguageServiceOnLanguageChanged;

        ApplicationLanguageOptions = new ReadOnlyCollection<ApplicationLanguageOption>(
            [
                new ApplicationLanguageOption(this, ApplicationLanguageTags.System),
                .. _applicationLanguageService.SupportedCultures
                    .Select(static culture => culture.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(languageTag => new ApplicationLanguageOption(this, languageTag)),
            ]);
        PromptLanguageOptions = new ReadOnlyCollection<ApplicationLanguageOption>(
        [
            .. _applicationLanguageService.SupportedCultures
                .Select(static culture => culture.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(languageTag => new ApplicationLanguageOption(this, languageTag)),
        ]);
    }

    public event EventHandler? LanguageChanged;

    public ReadOnlyCollection<ApplicationLanguageOption> ApplicationLanguageOptions { get; }

    public ReadOnlyCollection<ApplicationLanguageOption> PromptLanguageOptions { get; }

    public string AppTitle => GetString(nameof(AppTitle), "TokenMap");
    public string PreviousFoldersTitle => GetString(nameof(PreviousFoldersTitle), "Previous folders");
    public string Clear => GetString(nameof(Clear), "Clear");
    public string NoPreviousFoldersYet => GetString(nameof(NoPreviousFoldersYet), "No previous folders yet");
    public string OpenFolder => GetString(nameof(OpenFolder), "Open Folder");
    public string Missing => GetString(nameof(Missing), "Missing");
    public string RemoveFromRecentFoldersTooltip => GetString(nameof(RemoveFromRecentFoldersTooltip), "Remove from recent folders");
    public string OpenLogs => GetString(nameof(OpenLogs), "Open Logs");
    public string Dismiss => GetString(nameof(Dismiss), "Dismiss");
    public string CloseApp => GetString(nameof(CloseApp), "Close App");
    public string Settings => GetString(nameof(Settings), "Settings");
    public string CloseSettingsTooltip => GetString(nameof(CloseSettingsTooltip), "Close settings");
    public string View => GetString(nameof(View), "View");
    public string TreemapPalette => GetString(nameof(TreemapPalette), "Treemap palette");
    public string TreemapPaletteWeighted => GetString(nameof(TreemapPaletteWeighted), "Weighted");
    public string TreemapPaletteStudio => GetString(nameof(TreemapPaletteStudio), "Studio");
    public string TreemapPalettePlain => GetString(nameof(TreemapPalettePlain), "Plain");
    public string Metrics => GetString(nameof(Metrics), "Metrics");
    public string TreeOnly => GetString(nameof(TreeOnly), "Tree only");
    public string Scan => GetString(nameof(Scan), "Scan");
    public string RespectGitIgnore => GetString(nameof(RespectGitIgnore), "Respect .gitignore");
    public string UseGlobalExcludes => GetString(nameof(UseGlobalExcludes), "Use global excludes");
    public string Edit => GetString(nameof(Edit), "Edit");
    public string UseFolderExcludes => GetString(nameof(UseFolderExcludes), "Use folder excludes");
    public string ScanSettingsUpdatedRescanToApply => GetString(nameof(ScanSettingsUpdatedRescanToApply), "Scan settings updated. Rescan to apply.");
    public string RescanNow => GetString(nameof(RescanNow), "Rescan now");
    public string Prompts => GetString(nameof(Prompts), "Prompts");
    public string EditRefactorPromptTemplate => GetString(nameof(EditRefactorPromptTemplate), "Edit refactor prompt template");
    public string Theme => GetString(nameof(Theme), "Theme");
    public string UseLightTheme => GetString(nameof(UseLightTheme), "Use the light theme");
    public string UseSystemTheme => GetString(nameof(UseSystemTheme), "Follow the system theme");
    public string UseDarkTheme => GetString(nameof(UseDarkTheme), "Use the dark theme");
    public string ProjectTree => GetString(nameof(ProjectTree), "Project Tree");
    public string Name => GetString(nameof(Name), "Name");
    public string PercentParent => GetString(nameof(PercentParent), "% Parent");
    public string Treemap => GetString(nameof(Treemap), "Treemap");
    public string ShowValues => GetString(nameof(ShowValues), "Show values");
    public string Threshold => GetString(nameof(Threshold), "Threshold");
    public string OpenAction => GetString(nameof(OpenAction), "Open");
    public string PreviewAction => GetString(nameof(PreviewAction), "Preview");
    public string RefactorPromptAction => GetString(nameof(RefactorPromptAction), "Refactor Prompt");
    public string SetAsTreemapRootAction => GetString(nameof(SetAsTreemapRootAction), "Set as Treemap Root");
    public string ExcludeFromScanAction => GetString(nameof(ExcludeFromScanAction), "Exclude from Scan");
    public string CopyFullPathAction => GetString(nameof(CopyFullPathAction), "Copy Full Path");
    public string CopyRelativePathAction => GetString(nameof(CopyRelativePathAction), "Copy Relative Path");
    public string OpenFolderForAnalysisTooltip => GetString(nameof(OpenFolderForAnalysisTooltip), "Open a folder for analysis");
    public string RunScanAgainTooltip => GetString(nameof(RunScanAgainTooltip), "Run the scan again for the selected folder");
    public string StopCurrentScanTooltip => GetString(nameof(StopCurrentScanTooltip), "Stop the current scan");
    public string Rescan => GetString(nameof(Rescan), "Rescan");
    public string Stop => GetString(nameof(Stop), "Stop");
    public string Tokens => GetString(nameof(Tokens), "Tokens");
    public string Lines => GetString(nameof(Lines), "Lines");
    public string Files => GetString(nameof(Files), "Files");
    public string Share => GetString(nameof(Share), "Share");
    public string PreviewAndCopyShareImageTooltip => GetString(nameof(PreviewAndCopyShareImageTooltip), "Preview and copy a share image");
    public string OpenSettingsTooltip => GetString(nameof(OpenSettingsTooltip), "Open settings");
    public string ShareImage => GetString(nameof(ShareImage), "Share image");
    public string CloseShareImagePreviewTooltip => GetString(nameof(CloseShareImagePreviewTooltip), "Close share image preview");
    public string ShareImagePreviewHint => GetString(nameof(ShareImagePreviewHint), "Preview the card, optionally include a project name, then copy the image to the clipboard.");
    public string IncludeProjectName => GetString(nameof(IncludeProjectName), "Include project name");
    public string ProjectNameWatermark => GetString(nameof(ProjectNameWatermark), "Project name");
    public string CloseFilePreviewTooltip => GetString(nameof(CloseFilePreviewTooltip), "Close file preview");
    public string Explainability => GetString(nameof(Explainability), "Explainability");
    public string Close => GetString(nameof(Close), "Close");
    public string CancelEditingExcludesTooltip => GetString(nameof(CancelEditingExcludesTooltip), "Cancel editing excludes");
    public string Cancel => GetString(nameof(Cancel), "Cancel");
    public string Save => GetString(nameof(Save), "Save");
    public string SaveAndRescan => GetString(nameof(SaveAndRescan), "Save and Rescan");
    public string RefactorPrompt => GetString(nameof(RefactorPrompt), "Refactor prompt");
    public string CloseRefactorPromptTooltip => GetString(nameof(CloseRefactorPromptTooltip), "Close refactor prompt");
    public string RefactorPromptIntro => GetString(nameof(RefactorPromptIntro), "Edit the conversation starter if needed, then copy it into your coding agent.");
    public string RefactorPromptHint => GetString(nameof(RefactorPromptHint), "This prompt frames the file as a possible refactoring candidate without prescribing a specific implementation.");
    public string RefactorPromptTemplate => GetString(nameof(RefactorPromptTemplate), "Refactor prompt template");
    public string RefactorPromptTemplateScope => GetString(nameof(RefactorPromptTemplateScope), "Global setting for all repositories on this machine.");
    public string CloseRefactorPromptTemplateEditorTooltip => GetString(nameof(CloseRefactorPromptTemplateEditorTooltip), "Close refactor prompt template editor");
    public string Placeholders => GetString(nameof(Placeholders), "Placeholders");
    public string PlaceholdersHint => GetString(nameof(PlaceholdersHint), "Insert these tokens anywhere in the template.");
    public string RefactorPromptTemplateEditorHint => GetString(nameof(RefactorPromptTemplateEditorHint), "Edit the default conversation-starter template used for new refactor prompts.");
    public string ResetToDefault => GetString(nameof(ResetToDefault), "Reset to default");
    public string PromptLanguage => GetString(nameof(PromptLanguage), "Prompt language");
    public string ApplicationLanguage => GetString(nameof(ApplicationLanguage), "Application language");
    public string SystemLanguage => GetString(nameof(SystemLanguage), "System");
    public string EnglishLanguage => GetString(nameof(EnglishLanguage), "English");
    public string RussianLanguage => GetString(nameof(RussianLanguage), "Russian");
    public string Copy => GetString(nameof(Copy), "Copy");
    public string Copied => GetString(nameof(Copied), "Copied");
    public string RevealAction => GetString(nameof(RevealAction), "Reveal");
    public string ShareCardTokens => GetString(nameof(ShareCardTokens), "tokens");
    public string ShareCardLines => GetString(nameof(ShareCardLines), "lines");
    public string ShareCardFiles => GetString(nameof(ShareCardFiles), "files");
    public string ShareCardMadeWith => GetString(nameof(ShareCardMadeWith), "made with");

    public string FormatCurrentFolderSettingsTitle(string? folderName) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatCurrentFolderSettingsTitle), "Current folder: {0}"),
            folderName ?? string.Empty);

    public string CurrentFolderSettingsTitleFallback => GetString(nameof(CurrentFolderSettingsTitleFallback), "Current folder");

    public string WorkspaceLayoutToggleToolTip(bool isStackedWorkspaceLayout) =>
        isStackedWorkspaceLayout
            ? GetString("WorkspaceLayoutToggleToolTipSideBySide", "Switch to side-by-side layout")
            : GetString("WorkspaceLayoutToggleToolTipStacked", "Switch to stacked layout");

    public string FatalIssueTitle => GetString(nameof(FatalIssueTitle), "Unrecoverable application error");
    public string NonFatalIssueTitle => GetString(nameof(NonFatalIssueTitle), "Application issue");

    public string FormatReferenceId(string referenceId) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatReferenceId), "Reference ID: {0}"),
            referenceId);

    public string AboutFallbackDescription => GetString(nameof(AboutFallbackDescription), "Local source-tree analysis");
    public string AboutLicenseName => GetString(nameof(AboutLicenseName), "MIT license");
    public string FormatVersion(string version) =>
        string.Format(EffectiveCulture, GetString("FormatVersion", "v{0}"), version);

    public string AboutOpenRepositoryFailed => GetString(nameof(AboutOpenRepositoryFailed), "TokenMap could not open the project repository link.");
    public string OpenLogsFailed => GetString(nameof(OpenLogsFailed), "TokenMap could not open the diagnostics log folder.");
    public string ShareCopyImageFailed => GetString(nameof(ShareCopyImageFailed), "TokenMap could not copy the share image to the clipboard.");
    public string RefactorPromptCopyFailed => GetString(nameof(RefactorPromptCopyFailed), "TokenMap could not copy the refactor prompt to the clipboard.");
    public string OpenNodeFailed(string nodeName) =>
        string.Format(EffectiveCulture, GetString(nameof(OpenNodeFailed), "TokenMap could not open '{0}'."), nodeName);
    public string RevealNodeFailed(string nodeName) =>
        string.Format(EffectiveCulture, GetString(nameof(RevealNodeFailed), "TokenMap could not reveal '{0}'."), nodeName);
    public string FormatAnalysisRootMissing(string folderPath) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatAnalysisRootMissing), "TokenMap could not find '{0}'."),
            folderPath);
    public string FormatAnalysisRunFailed(string folderPath) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatAnalysisRunFailed), "TokenMap could not finish analyzing '{0}'."),
            folderPath);
    public string RecentFoldersEmptyFlyoutSecondaryText =>
        GetString(nameof(RecentFoldersEmptyFlyoutSecondaryText), "Analyze a folder once and it will appear here.");
    public string StartupFailedToStart =>
        GetString(nameof(StartupFailedToStart), "TokenMap failed to start.");
    public string StartupDiagnosticDetailsWrittenTo =>
        GetString(nameof(StartupDiagnosticDetailsWrittenTo), "Diagnostic details were written to:");

    public string DispatcherUnhandledUserMessage => GetString(nameof(DispatcherUnhandledUserMessage), "TokenMap hit an unexpected error. Review the log details before continuing.");
    public string UnobservedTaskUserMessage => GetString(nameof(UnobservedTaskUserMessage), "A background task failed. TokenMap wrote diagnostic details to the log.");
    public string DomainUnhandledUserMessage => GetString(nameof(DomainUnhandledUserMessage), "TokenMap hit an unrecoverable error. Review the log details and restart the app.");

    public string SummaryIdle => GetString(nameof(SummaryIdle), "Select a folder to build a project treemap and metrics snapshot.");
    public string SummaryScanning => GetString(nameof(SummaryScanning), "Analyzing project structure, token counts and non-empty line statistics.");
    public string SummaryCancelled => GetString(nameof(SummaryCancelled), "Analysis was cancelled. Previous snapshot remains available.");
    public string SummaryFailed => GetString(nameof(SummaryFailed), "Analysis failed. Review the diagnostics issue and log details.");
    public string FormatSummaryCompleted(string rootName) =>
        string.Format(EffectiveCulture, GetString(nameof(FormatSummaryCompleted), "Analysis completed for {0}."), rootName);
    public string FormatSummaryCompletedWithDiagnostics(string rootName, int diagnosticCount) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatSummaryCompletedWithDiagnostics), "Analysis completed for {0} with {1:N0} diagnostics."),
            rootName,
            diagnosticCount);
    public string FormatTotalsText(long tokenCount, long lineCount, long fileCount, int diagnosticCount) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatTotalsText), "{0:N0} tokens - {1:N0} non-empty lines - {2:N0} files - {3:N0} diagnostics"),
            tokenCount,
            lineCount,
            fileCount,
            diagnosticCount);
    public string ProgressScanningTree => GetString(nameof(ProgressScanningTree), "Scanning tree");
    public string ProgressAnalyzingFiles => GetString(nameof(ProgressAnalyzingFiles), "Analyzing files");
    public string FormatProgressAnalyzingFiles(long processedCount, long totalCount) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatProgressAnalyzingFiles), "Analyzing files • {0:N0} / {1:N0}"),
            processedCount,
            totalCount);
    public string FormatProgressScanningTreeFilesFound(long fileCount) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatProgressScanningTreeFilesFound), "Scanning tree • {0:N0} files found"),
            fileCount);

    public string GlobalExcludesTitle => GetString(nameof(GlobalExcludesTitle), "Global excludes");
    public string GlobalExcludesHelperText => GetString(nameof(GlobalExcludesHelperText), "Use gitignore-style rules, one per line. Use / for project-root rules, ! for re-include rules, and # for comments.");
    public string FolderExcludesHelperText => GetString(nameof(FolderExcludesHelperText), "Use gitignore-style rules, one per line. These rules apply only to the current folder and override .gitignore. Use / for folder-root rules, ! for re-include rules, and # for comments.");
    public string FormatFolderExcludesTitle(string folderName) =>
        string.Format(
            EffectiveCulture,
            GetString(nameof(FormatFolderExcludesTitle), "Excludes for {0}"),
            folderName);

    public string LoadingPreviewTitle => GetString(nameof(LoadingPreviewTitle), "Loading preview");
    public string LoadingPreviewMessage => GetString(nameof(LoadingPreviewMessage), "Reading file contents.");
    public string PreviewUnavailableTitle => GetString(nameof(PreviewUnavailableTitle), "Preview unavailable");
    public string PreviewUnavailableMessage => GetString(nameof(PreviewUnavailableMessage), "TokenMap only shows text files in the built-in preview.");
    public string FileTooLargeTitle => GetString(nameof(FileTooLargeTitle), "File too large");
    public string FileTooLargeMessage => GetString(nameof(FileTooLargeMessage), "This file is larger than 2 MiB. Open it in the default app to inspect the full contents.");
    public string FileMissingTitle => GetString(nameof(FileMissingTitle), "File missing");
    public string FileMissingMessage => GetString(nameof(FileMissingMessage), "The file is no longer available at its original path.");
    public string PreviewFailedTitle => GetString(nameof(PreviewFailedTitle), "Preview failed");
    public string PreviewFailedMessage => GetString(nameof(PreviewFailedMessage), "TokenMap could not read this file.");

    public string ExplainabilityMetricUnavailable => GetString(nameof(ExplainabilityMetricUnavailable), "This metric is unavailable for this file.");
    public string ExplainabilityGitUnavailableNote => GetString(nameof(ExplainabilityGitUnavailableNote), "Git context unavailable. Refactor Priority currently equals the structural base.");
    public string ExplainabilityGitBelowThresholdsNote => GetString(nameof(ExplainabilityGitBelowThresholdsNote), "Git context is available, but all git signals stayed below the uplift thresholds.");
    public string ExplainabilityStructuralBaseTitle => GetString(nameof(ExplainabilityStructuralBaseTitle), "Structural base");
    public string ExplainabilityStructuralBaseNote => GetString(nameof(ExplainabilityStructuralBaseNote), "Intrinsic score from file scale, callable burden, and risk distribution.");
    public string ExplainabilityStructuralUnavailableNote => GetString(nameof(ExplainabilityStructuralUnavailableNote), "Structural breakdown unavailable for this file.");
    public string ExplainabilityGitUpliftTitle => GetString(nameof(ExplainabilityGitUpliftTitle), "Git uplift");
    public string ExplainabilityGitUpliftPositiveNote => GetString(nameof(ExplainabilityGitUpliftPositiveNote), "Bounded urgency boost from recent change and co-change pressure.");
    public string ExplainabilityGitUnavailableForFileNote => GetString(nameof(ExplainabilityGitUnavailableForFileNote), "Git context unavailable for this file.");
    public string ExplainabilityNotAvailable => GetString(nameof(ExplainabilityNotAvailable), "n/a");

    public string RefactorPromptStructuralBaseDrivers => GetString(nameof(RefactorPromptStructuralBaseDrivers), "Structural base drivers");
    public string RefactorPromptGitDrivers => GetString(nameof(RefactorPromptGitDrivers), "Git drivers");
    public string RefactorPromptGitUnavailableLine => GetString(nameof(RefactorPromptGitUnavailableLine), "Git uplift: unavailable because git-derived change and co-change inputs were not produced for this file.");
    public string RefactorPromptGitZeroLine => GetString(nameof(RefactorPromptGitZeroLine), "Git uplift: +0 pts because all git signals stayed below the uplift thresholds.");

    public string PromptTemplatePlaceholderRelativePath => GetString(nameof(PromptTemplatePlaceholderRelativePath), "Relative path of the selected file.");
    public string PromptTemplatePlaceholderTokens => GetString(nameof(PromptTemplatePlaceholderTokens), "Token count for the file.");
    public string PromptTemplatePlaceholderNonEmptyLines => GetString(nameof(PromptTemplatePlaceholderNonEmptyLines), "Non-empty line count.");
    public string PromptTemplatePlaceholderFileSize => GetString(nameof(PromptTemplatePlaceholderFileSize), "File size, formatted for display.");
    public string PromptTemplatePlaceholderRefactorPriority => GetString(nameof(PromptTemplatePlaceholderRefactorPriority), "Composite refactor-priority score.");
    public string PromptTemplatePlaceholderRefactorPriorityBreakdown => GetString(nameof(PromptTemplatePlaceholderRefactorPriorityBreakdown), "Multi-line explanation for the structural base and any git-derived uplift behind refactor priority.");

    public string GetApplicationLanguageLabel(string languageTag)
    {
        if (ApplicationLanguageTags.IsSystem(languageTag))
        {
            return SystemLanguage;
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(languageTag);
            return FormatLanguageLabel(culture);
        }
        catch (CultureNotFoundException)
        {
            return languageTag;
        }
    }

    public string GetRevealMenuHeader(string shellHeader) =>
        shellHeader switch
        {
            "Reveal in Explorer" => GetString("RevealInExplorerAction", "Reveal in Explorer"),
            "Reveal in Finder" => GetString("RevealInFinderAction", "Reveal in Finder"),
            "Reveal in File Manager" => GetString("RevealInFileManagerAction", "Reveal in File Manager"),
            _ => RevealAction,
        };

    public string GetMetricDisplayName(string metricKey, string fallback) => GetString($"{metricKey}DisplayName", fallback);
    public string GetMetricShortName(string metricKey, string fallback) => GetString($"{metricKey}ShortName", fallback);
    public string GetMetricDescription(string metricKey, string fallback) => GetString($"{metricKey}Description", fallback);

    public string GetTreemapPlaceholderNoSnapshot() => GetString(nameof(GetTreemapPlaceholderNoSnapshot), "Run analysis to populate treemap.");
    public string GetTreemapPlaceholderNoWeightedNodes() => GetString(nameof(GetTreemapPlaceholderNoWeightedNodes), "No weighted nodes for the selected metric.");
    public string TreemapRootPath => GetString(nameof(TreemapRootPath), "(root)");
    public string TreemapNotAvailable => GetString(nameof(TreemapNotAvailable), "n/a");
    public string TreemapShare => GetString(nameof(TreemapShare), "Share");
    public string TreemapType => GetString(nameof(TreemapType), "Type");
    public string TreemapExtension => GetString(nameof(TreemapExtension), "Ext");
    public string TreemapNoExtension => GetString(nameof(TreemapNoExtension), "(none)");
    public string TreemapFilesInSubtree => GetString(nameof(TreemapFilesInSubtree), "Files in subtree");
    public string TreemapKindRoot => GetString(nameof(TreemapKindRoot), "Root");
    public string TreemapKindDirectory => GetString(nameof(TreemapKindDirectory), "Directory");
    public string TreemapKindFile => GetString(nameof(TreemapKindFile), "File");

    public string GetStructuralDescription(string key) =>
        key switch
        {
            "code_lines" => GetString("Structural_code_lines", "Code volume in the file. Larger files tend to accumulate more moving parts before method-level risk is considered."),
            "total_callable_burden_points" => GetString("Structural_total_callable_burden_points", "Sum of per-callable burden after soft thresholds for method length, cyclomatic complexity, nesting depth, and parameter count."),
            "top_callable_burden_points" => GetString("Structural_top_callable_burden_points", "Burden of the single heaviest callable. This catches one dominant method even when the rest of the file looks moderate."),
            "affected_callable_ratio" => GetString("Structural_affected_callable_ratio", "Share of callables that exceed the soft thresholds. Higher ratios mean the problem is spread across the file rather than isolated."),
            "top_three_callable_burden_share" => GetString("Structural_top_three_callable_burden_share", "Share of callable burden concentrated in the top three callables. High concentration means a small number of methods dominate the risk."),
            _ => GetString("DefaultStructuralDescription", "Structural input that feeds the intrinsic refactor-risk base score."),
        };

    public string GetGitDescription(string key) =>
        key switch
        {
            "churn_lines_90d" => GetString("Git_churn_lines_90d", "Recently rewritten line volume. Frequent rewrites raise urgency, but only as a bounded uplift on top of the structural base."),
            "touch_count_90d" => GetString("Git_touch_count_90d", "Number of recent commits that touched this file. Repeated touches suggest ongoing friction around the code."),
            "author_count_90d" => GetString("Git_author_count_90d", "Number of recent contributors touching the file. More contributors usually increase coordination pressure around risky code."),
            "strong_cochanged_file_count_90d" => GetString("Git_strong_cochanged_file_count_90d", "Files that repeatedly change together with this one. This indicates a tighter blast radius when the file moves."),
            "unique_cochanged_file_count_90d" => GetString("Git_unique_cochanged_file_count_90d", "Breadth of different files that changed alongside this one across the recent history window."),
            "avg_cochange_set_size_90d" => GetString("Git_avg_cochange_set_size_90d", "Typical width of change sets that include this file. Wider sets suggest changes tend to propagate."),
            _ => GetString("DefaultGitDescription", "Git-derived pressure that can amplify urgency without dominating the structural base."),
        };

    public string EffectiveLanguageName => _applicationLanguageService.EffectiveCulture.Name;
    public CultureInfo EffectiveCulture => _applicationLanguageService.EffectiveCulture;

    private void ApplicationLanguageServiceOnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var option in ApplicationLanguageOptions)
        {
            option.Refresh();
        }

        foreach (var option in PromptLanguageOptions)
        {
            option.Refresh();
        }

        foreach (var propertyName in ObservablePropertyNames)
        {
            OnPropertyChanged(propertyName);
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private string GetString(string resourceName, string fallback)
    {
        string? value;
        try
        {
            value = ResourceManager.GetString(resourceName, EffectiveCulture);
        }
        catch (MissingManifestResourceException)
        {
            value = null;
        }

        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value;
    }

    private static string FormatLanguageLabel(CultureInfo culture)
    {
        var label = culture.NativeName;
        if (string.IsNullOrWhiteSpace(label))
        {
            return culture.Name;
        }

        return char.ToUpper(label[0], culture) + label[1..];
    }
}

public sealed class ApplicationLanguageOption(LocalizationState localization, string value) : ObservableObject
{
    private readonly LocalizationState _localization = localization;

    public string Value { get; } = value;

    public string Label => _localization.GetApplicationLanguageLabel(Value);

    internal void Refresh() => OnPropertyChanged(nameof(Label));
}
