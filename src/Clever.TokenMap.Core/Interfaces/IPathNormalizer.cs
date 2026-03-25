namespace Clever.TokenMap.Core.Interfaces;

public interface IPathNormalizer
{
    StringComparer PathComparer { get; }

    string NormalizeRootPath(string rootPath);

    string NormalizeFullPath(string path);

    string NormalizeRelativePath(string rootPath, string path);
}
