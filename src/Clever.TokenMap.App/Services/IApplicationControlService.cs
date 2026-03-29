namespace Clever.TokenMap.App.Services;

public interface IApplicationControlService
{
    void RequestShutdown(int exitCode = 0);
}
