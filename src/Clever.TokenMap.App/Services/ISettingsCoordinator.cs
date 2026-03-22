using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.ViewModels;

namespace Clever.TokenMap.App.Services;

public interface ISettingsCoordinator
{
    void Attach(ToolbarViewModel toolbar);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
