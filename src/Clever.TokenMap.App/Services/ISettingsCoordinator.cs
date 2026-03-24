using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface ISettingsCoordinator : IScanOptionsResolver
{
    SettingsState State { get; }

    CurrentFolderSettingsState CurrentFolderState { get; }

    void SwitchActiveFolder(string? rootPath);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
