using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface IFilePreviewController
{
    FilePreviewState State { get; }

    Task OpenAsync(ProjectNode? node, CancellationToken cancellationToken = default);

    void Close();
}
