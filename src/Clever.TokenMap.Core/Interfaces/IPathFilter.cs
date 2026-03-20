namespace Clever.TokenMap.Core.Interfaces;

public interface IPathFilter
{
    bool IsIncluded(string fullPath, string normalizedRelativePath, bool isDirectory);
}
