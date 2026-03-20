namespace Clever.TokenMap.Core.Interfaces;

public interface ITextFileDetector
{
    ValueTask<bool> IsTextAsync(string fullPath, CancellationToken cancellationToken);
}
