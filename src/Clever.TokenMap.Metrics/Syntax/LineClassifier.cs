namespace Clever.TokenMap.Metrics.Syntax;

internal static class LineClassifier
{
    public static LineClassificationResult Classify(
        string sourceText,
        IEnumerable<TextSpan> commentSpans)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(commentSpans);

        if (sourceText.Length == 0)
        {
            return default;
        }

        var normalizedCommentSpans = commentSpans
            .Where(span => span.EndIndex > span.StartIndex)
            .OrderBy(span => span.StartIndex)
            .ToArray();
        var lineRanges = BuildLineRanges(sourceText);

        var codeLineCount = 0;
        var commentLineCount = 0;
        foreach (var lineRange in lineRanges)
        {
            var overlappingCommentSpans = GetOverlappingSpans(lineRange, normalizedCommentSpans);
            if (overlappingCommentSpans.Count == 0)
            {
                if (ContainsNonWhitespace(sourceText, lineRange.StartIndex, lineRange.EndIndex))
                {
                    codeLineCount++;
                }

                continue;
            }

            var mergedSpans = MergeSpans(overlappingCommentSpans);
            var hasCommentContent = mergedSpans.Any(span => ContainsNonWhitespace(sourceText, span.StartIndex, span.EndIndex));
            var hasCodeContent = ContainsNonWhitespaceOutside(sourceText, lineRange, mergedSpans);
            if (hasCodeContent)
            {
                codeLineCount++;
            }

            if (hasCommentContent && !hasCodeContent)
            {
                commentLineCount++;
            }
        }

        return new LineClassificationResult(codeLineCount, commentLineCount);
    }

    private static List<TextSpan> BuildLineRanges(string sourceText)
    {
        var lineRanges = new List<TextSpan>();
        var lineStart = 0;
        for (var index = 0; index < sourceText.Length; index++)
        {
            var character = sourceText[index];
            if (character != '\r' && character != '\n')
            {
                continue;
            }

            lineRanges.Add(new TextSpan(lineStart, index));
            if (character == '\r' &&
                index + 1 < sourceText.Length &&
                sourceText[index + 1] == '\n')
            {
                index++;
            }

            lineStart = index + 1;
        }

        lineRanges.Add(new TextSpan(lineStart, sourceText.Length));
        return lineRanges;
    }

    private static List<TextSpan> GetOverlappingSpans(TextSpan lineRange, IReadOnlyList<TextSpan> commentSpans)
    {
        var result = new List<TextSpan>();
        foreach (var commentSpan in commentSpans)
        {
            if (commentSpan.EndIndex <= lineRange.StartIndex)
            {
                continue;
            }

            if (commentSpan.StartIndex >= lineRange.EndIndex)
            {
                break;
            }

            var startIndex = Math.Max(lineRange.StartIndex, commentSpan.StartIndex);
            var endIndex = Math.Min(lineRange.EndIndex, commentSpan.EndIndex);
            if (endIndex > startIndex)
            {
                result.Add(new TextSpan(startIndex, endIndex));
            }
        }

        return result;
    }

    private static List<TextSpan> MergeSpans(List<TextSpan> spans)
    {
        if (spans.Count <= 1)
        {
            return spans.ToList();
        }

        var mergedSpans = new List<TextSpan>(spans.Count);
        var current = spans[0];
        for (var index = 1; index < spans.Count; index++)
        {
            var next = spans[index];
            if (next.StartIndex <= current.EndIndex)
            {
                current = new TextSpan(current.StartIndex, Math.Max(current.EndIndex, next.EndIndex));
                continue;
            }

            mergedSpans.Add(current);
            current = next;
        }

        mergedSpans.Add(current);
        return mergedSpans;
    }

    private static bool ContainsNonWhitespaceOutside(
        string sourceText,
        TextSpan lineRange,
        IReadOnlyList<TextSpan> excludedSpans)
    {
        var cursor = lineRange.StartIndex;
        foreach (var excludedSpan in excludedSpans)
        {
            if (excludedSpan.StartIndex > cursor &&
                ContainsNonWhitespace(sourceText, cursor, excludedSpan.StartIndex))
            {
                return true;
            }

            cursor = Math.Max(cursor, excludedSpan.EndIndex);
        }

        return cursor < lineRange.EndIndex &&
               ContainsNonWhitespace(sourceText, cursor, lineRange.EndIndex);
    }

    private static bool ContainsNonWhitespace(string sourceText, int startIndex, int endIndex)
    {
        for (var index = startIndex; index < endIndex; index++)
        {
            if (!char.IsWhiteSpace(sourceText[index]))
            {
                return true;
            }
        }

        return false;
    }
}
