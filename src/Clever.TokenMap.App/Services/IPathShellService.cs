using System.Threading;
using System.Threading.Tasks;

namespace Clever.TokenMap.App.Services;

public interface IPathShellService
{
    string RevealMenuHeader { get; }

    Task<bool> TryOpenAsync(string fullPath, CancellationToken cancellationToken = default);

    Task<bool> TryRevealAsync(string fullPath, bool isDirectory, CancellationToken cancellationToken = default);
}
