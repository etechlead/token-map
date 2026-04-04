using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.Core.Preview;

namespace Clever.TokenMap.Core.Interfaces;

public interface IFilePreviewContentReader
{
    Task<FilePreviewContentResult> ReadAsync(string fullPath, CancellationToken cancellationToken = default);
}
