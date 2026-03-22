using Clever.TokenMap.App.State;

namespace Clever.TokenMap.App.Services;

public interface ISettingsCoordinator
{
    SettingsState State { get; }
}
