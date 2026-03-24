using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Settings;

public static class FolderSettingsStorageKey
{
    private const int MaxKeyLength = 64;
    private const int HashLength = 12;
    private static readonly Regex InvalidSlugCharacters = new(@"[^a-z0-9]+", RegexOptions.Compiled);

    public static string Build(string rootPath, PathNormalizer? pathNormalizer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizer = pathNormalizer ?? new PathNormalizer();
        var normalizedRootPath = normalizer.NormalizeRootPath(rootPath);
        var canonicalIdentity = GetCanonicalIdentity(normalizedRootPath);
        var slug = BuildSlug(normalizedRootPath);
        var hash = ComputeHashSuffix(canonicalIdentity);
        var maxSlugLength = MaxKeyLength - HashLength - 1;

        if (slug.Length > maxSlugLength)
        {
            slug = slug[..maxSlugLength].Trim('-');
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "folder";
        }

        return $"{slug}-{hash}";
    }

    internal static string GetCanonicalIdentity(string normalizedRootPath) =>
        OperatingSystem.IsWindows()
            ? normalizedRootPath.ToUpperInvariant()
            : normalizedRootPath;

    private static string BuildSlug(string normalizedRootPath)
    {
        var slug = normalizedRootPath
            .Replace('\\', '/')
            .ToLowerInvariant();

        slug = InvalidSlugCharacters.Replace(slug, "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "folder" : slug;
    }

    private static string ComputeHashSuffix(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..HashLength].ToLowerInvariant();
    }
}
