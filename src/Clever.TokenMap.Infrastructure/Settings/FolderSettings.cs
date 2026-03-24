using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Infrastructure.Settings;

public sealed class FolderSettings
{
    public string RootPath { get; set; } = string.Empty;

    public FolderScanSettings Scan { get; set; } = new();

    public static FolderSettings CreateDefault(string? rootPath = null) =>
        new()
        {
            RootPath = rootPath?.Trim() ?? string.Empty,
        };

    public FolderSettings Clone() =>
        new()
        {
            RootPath = RootPath,
            Scan = Scan.Clone(),
        };
}

public sealed class FolderScanSettings
{
    public bool UseFolderExcludes { get; set; }

    public List<string> FolderExcludes { get; set; } = [];

    public FolderScanSettings Clone() =>
        new()
        {
            UseFolderExcludes = UseFolderExcludes,
            FolderExcludes = [.. FolderExcludes],
        };

    public static FolderScanSettings Normalize(FolderScanSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new FolderScanSettings
        {
            UseFolderExcludes = settings.UseFolderExcludes,
            FolderExcludes = [.. GlobalExcludeList.Normalize(settings.FolderExcludes)],
        };
    }
}
