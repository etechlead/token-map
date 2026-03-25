using System;
using System.Threading.Tasks;

namespace Clever.TokenMap.App.Services;

internal static class SettingsPersistenceQueue
{
    public static async Task QueueAsync(Task previousTask, Action persistenceAction)
    {
        ArgumentNullException.ThrowIfNull(previousTask);
        ArgumentNullException.ThrowIfNull(persistenceAction);

        try
        {
            await previousTask.ConfigureAwait(false);
        }
        catch
        {
        }

        await Task.Run(persistenceAction).ConfigureAwait(false);
    }
}
