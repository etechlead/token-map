using System.Threading;
using System.Threading.Tasks;

namespace Clever.TokenMap.App.Services;

public interface IFolderPickerService
{
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);
}
