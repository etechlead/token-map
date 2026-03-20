using Clever.TokenMap.Core.Interfaces;

namespace Clever.TokenMap.Infrastructure.Text;

public sealed class HeuristicTextFileDetector : ITextFileDetector
{
    private const int SampleSize = 4 * 1024;
    private const double SuspiciousByteThreshold = 0.10d;

    public async ValueTask<bool> IsTextAsync(string fullPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: SampleSize,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[SampleSize];
        var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, SampleSize), cancellationToken);

        if (bytesRead == 0)
        {
            return true;
        }

        var suspiciousByteCount = 0;

        for (var index = 0; index < bytesRead; index++)
        {
            var current = buffer[index];
            if (current == 0)
            {
                return false;
            }

            if (IsSuspiciousControlByte(current))
            {
                suspiciousByteCount++;
            }
        }

        return suspiciousByteCount / (double)bytesRead <= SuspiciousByteThreshold;
    }

    private static bool IsSuspiciousControlByte(byte value) =>
        value < 0x20 &&
        value is not (byte)'\t' and not (byte)'\n' and not (byte)'\r' and not (byte)'\f';
}
