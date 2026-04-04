using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Preview;

namespace Clever.TokenMap.Infrastructure.Text;

public sealed class FilePreviewContentReader(ITextFileDetector textFileDetector) : IFilePreviewContentReader
{
    internal const long MaxPreviewFileSizeBytes = 2 * 1024 * 1024;

    private readonly ITextFileDetector _textFileDetector = textFileDetector;

    public async Task<FilePreviewContentResult> ReadAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                return new FilePreviewContentResult(FilePreviewReadStatus.Missing);
            }

            if (fileInfo.Length > MaxPreviewFileSizeBytes)
            {
                return new FilePreviewContentResult(FilePreviewReadStatus.TooLarge);
            }

            if (!await _textFileDetector.IsTextAsync(fullPath, cancellationToken).ConfigureAwait(false))
            {
                return new FilePreviewContentResult(FilePreviewReadStatus.NotText);
            }

            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 16 * 1024,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(
                stream,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 16 * 1024,
                leaveOpen: false);

            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return new FilePreviewContentResult(FilePreviewReadStatus.Success, Content: content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            return new FilePreviewContentResult(FilePreviewReadStatus.Missing);
        }
        catch (DirectoryNotFoundException)
        {
            return new FilePreviewContentResult(FilePreviewReadStatus.Missing);
        }
        catch (IOException ex)
        {
            return new FilePreviewContentResult(FilePreviewReadStatus.ReadFailed, ErrorMessage: ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new FilePreviewContentResult(FilePreviewReadStatus.ReadFailed, ErrorMessage: ex.Message);
        }
        catch (DecoderFallbackException ex)
        {
            return new FilePreviewContentResult(FilePreviewReadStatus.ReadFailed, ErrorMessage: ex.Message);
        }
    }
}
