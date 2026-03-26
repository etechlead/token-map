namespace Clever.TokenMap.Core.Interfaces;

public interface IPathFilter
{
    bool IsIncluded(string normalizedRelativePath);
}
