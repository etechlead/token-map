using Clever.TokenMap.Core.Interfaces;

namespace Clever.TokenMap.Infrastructure.Filtering;

public sealed class AllowAllPathFilter : IPathFilter
{
    public bool IsIncluded(string fullPath, string normalizedRelativePath, bool isDirectory) => true;
}
