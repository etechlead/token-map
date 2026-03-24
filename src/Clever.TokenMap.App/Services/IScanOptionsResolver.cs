using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface IScanOptionsResolver
{
    ScanOptions Resolve(string? rootPath, ScanOptions baseOptions);
}
