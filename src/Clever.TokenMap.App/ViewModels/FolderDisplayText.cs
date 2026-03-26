using System.IO;

namespace Clever.TokenMap.App.ViewModels;

internal static class FolderDisplayText
{
    public static string GetFolderDisplayName(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        var trimmedPath = folderPath.Trim();
        var displayName = Path.GetFileName(trimmedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return string.IsNullOrWhiteSpace(displayName)
            ? trimmedPath
            : displayName;
    }
}
