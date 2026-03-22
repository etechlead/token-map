namespace Clever.TokenMap.App.ViewModels;

public sealed class RecentFolderItemViewModel
{
    public RecentFolderItemViewModel(
        string displayName,
        string fullPath,
        string? secondaryText = null,
        bool isMissing = false,
        bool canOpen = true,
        bool showFolderIcon = true)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        SecondaryText = string.IsNullOrWhiteSpace(secondaryText) ? fullPath : secondaryText;
        IsMissing = isMissing;
        CanOpen = canOpen;
        ShowFolderIcon = showFolderIcon;
    }

    public string DisplayName { get; }

    public string FullPath { get; }

    public string SecondaryText { get; }

    public bool IsMissing { get; }

    public bool CanOpen { get; }

    public bool ShowFolderIcon { get; }
}
