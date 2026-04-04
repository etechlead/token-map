namespace Clever.TokenMap.Core.Preview;

public sealed record FilePreviewContentResult(
    FilePreviewReadStatus Status,
    string? Content = null,
    string? ErrorMessage = null);
