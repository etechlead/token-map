using Clever.TokenMap.Core.Interfaces;

namespace Clever.TokenMap.Infrastructure.Paths;

public sealed class FileSystemFolderPathService : IFolderPathService
{
    public bool Exists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        return Directory.Exists(folderPath);
    }
}
