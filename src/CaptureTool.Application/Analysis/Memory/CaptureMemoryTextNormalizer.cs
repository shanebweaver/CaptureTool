using System.Globalization;
using System.Text;

namespace CaptureTool.Application.Analysis.Memory;

internal static class CaptureMemoryTextNormalizer
{
    public static CaptureMemoryNormalizedText Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string compatibilityNormalized = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(compatibilityNormalized.Length);
        bool hasTokenContent = false;
        bool pendingSeparator = false;

        foreach (Rune rune in compatibilityNormalized.EnumerateRunes())
        {
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            bool isTokenContent = Rune.IsLetterOrDigit(rune) || category is
                UnicodeCategory.NonSpacingMark or
                UnicodeCategory.SpacingCombiningMark or
                UnicodeCategory.EnclosingMark;
            if (isTokenContent)
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(Rune.ToLowerInvariant(rune).ToString());
                hasTokenContent = true;
                pendingSeparator = false;
            }
            else if (hasTokenContent)
            {
                pendingSeparator = true;
            }
        }

        string value = builder.ToString();
        string[] tokens = value.Length == 0
            ? []
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        return new CaptureMemoryNormalizedText(value, tokens);
    }

    public static bool ContainsPhrase(string normalizedText, string normalizedPhrase)
    {
        if (normalizedText.Length == 0 || normalizedPhrase.Length == 0)
        {
            return false;
        }

        int searchFrom = 0;
        while (searchFrom <= normalizedText.Length - normalizedPhrase.Length)
        {
            int match = normalizedText.IndexOf(
                normalizedPhrase,
                searchFrom,
                StringComparison.Ordinal);
            if (match < 0)
            {
                return false;
            }

            bool startsAtBoundary = match == 0 || normalizedText[match - 1] == ' ';
            int end = match + normalizedPhrase.Length;
            bool endsAtBoundary = end == normalizedText.Length || normalizedText[end] == ' ';
            if (startsAtBoundary && endsAtBoundary)
            {
                return true;
            }

            searchFrom = match + 1;
        }

        return false;
    }

    public static CaptureMemoryTokenMatch MatchTokens(
        IReadOnlyList<string> queryTokens,
        IReadOnlySet<string> fieldTokens,
        IReadOnlyList<string> orderedFieldTokens)
    {
        if (queryTokens.Count == 0 || fieldTokens.Count == 0)
        {
            return CaptureMemoryTokenMatch.None;
        }

        int fuzzyMatches = 0;
        foreach (string queryToken in queryTokens.Distinct(StringComparer.Ordinal))
        {
            if (fieldTokens.Contains(queryToken))
            {
                continue;
            }

            // Typo matching is deliberately conservative: only one query term may use a
            // single insertion, deletion, substitution, or adjacent transposition, and short
            // terms never use fuzzy matching. This keeps ranking explainable and deterministic.
            if (fuzzyMatches > 0 || CountRunes(queryToken) < 5 ||
                !orderedFieldTokens.Any(candidate => IsOneEditAway(queryToken, candidate)))
            {
                return CaptureMemoryTokenMatch.None;
            }

            fuzzyMatches++;
        }

        return fuzzyMatches == 0
            ? CaptureMemoryTokenMatch.Exact
            : CaptureMemoryTokenMatch.SingleTypo;
    }

    public static string CreateSafeSnippet(string source, string query)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        var builder = new StringBuilder(Math.Min(
            source.Length,
            Application.Abstractions.Analysis.Memory.CaptureMemoryMatchEvidence.MaximumSnippetLength));
        bool pendingSpace = false;
        foreach (Rune rune in source.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        string collapsed = builder.ToString().Trim();
        const int MaximumLength =
            Application.Abstractions.Analysis.Memory.CaptureMemoryMatchEvidence.MaximumSnippetLength;
        if (collapsed.Length <= MaximumLength)
        {
            return collapsed;
        }

        int match = collapsed.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase);
        if (match < 0)
        {
            try
            {
                CaptureMemoryNormalizedText normalizedSource = Normalize(collapsed);
                CaptureMemoryNormalizedText normalizedQuery = Normalize(query);
                int normalizedMatch = normalizedSource.Value.IndexOf(
                    normalizedQuery.Value,
                    StringComparison.Ordinal);
                if (normalizedMatch >= 0 && normalizedSource.Value.Length > 0)
                {
                    match = (int)Math.Round(
                        normalizedMatch / (double)normalizedSource.Value.Length * collapsed.Length,
                        MidpointRounding.ToZero);
                }
            }
            catch (ArgumentException)
            {
                match = -1;
            }
        }

        int start = match < 0
            ? 0
            : Math.Max(0, match - (MaximumLength / 3));
        if (start + MaximumLength > collapsed.Length)
        {
            start = collapsed.Length - MaximumLength;
        }

        if (start > 0 && char.IsLowSurrogate(collapsed[start]))
        {
            start++;
        }

        int length = Math.Min(MaximumLength, collapsed.Length - start);
        if (length > 0 && char.IsHighSurrogate(collapsed[start + length - 1]))
        {
            length--;
        }

        string snippet = collapsed.Substring(start, length);
        if (start > 0)
        {
            snippet = '…' + snippet[1..];
        }

        if (start + length < collapsed.Length)
        {
            snippet = snippet[..^1] + '…';
        }

        return snippet;
    }

    private static int CountRunes(string value)
    {
        int count = 0;
        foreach (Rune _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    private static bool IsOneEditAway(string left, string right)
    {
        Rune[] leftRunes = left.EnumerateRunes().ToArray();
        Rune[] rightRunes = right.EnumerateRunes().ToArray();
        int lengthDifference = leftRunes.Length - rightRunes.Length;
        if (Math.Abs(lengthDifference) > 1)
        {
            return false;
        }

        if (lengthDifference == 0)
        {
            int firstDifference = -1;
            int differences = 0;
            for (int index = 0; index < leftRunes.Length; index++)
            {
                if (leftRunes[index] == rightRunes[index])
                {
                    continue;
                }

                firstDifference = firstDifference < 0 ? index : firstDifference;
                differences++;
                if (differences > 2)
                {
                    return false;
                }
            }

            if (differences == 1)
            {
                return true;
            }

            return differences == 2 &&
                firstDifference + 1 < leftRunes.Length &&
                leftRunes[firstDifference] == rightRunes[firstDifference + 1] &&
                leftRunes[firstDifference + 1] == rightRunes[firstDifference];
        }

        Rune[] shorter = lengthDifference < 0 ? leftRunes : rightRunes;
        Rune[] longer = lengthDifference < 0 ? rightRunes : leftRunes;
        int shortIndex = 0;
        int longIndex = 0;
        bool skipped = false;
        while (shortIndex < shorter.Length && longIndex < longer.Length)
        {
            if (shorter[shortIndex] == longer[longIndex])
            {
                shortIndex++;
                longIndex++;
                continue;
            }

            if (skipped)
            {
                return false;
            }

            skipped = true;
            longIndex++;
        }

        return true;
    }
}

internal sealed record CaptureMemoryNormalizedText(string Value, string[] Tokens)
{
    public IReadOnlySet<string> TokenSet { get; } = Tokens.ToHashSet(StringComparer.Ordinal);
}

internal enum CaptureMemoryTokenMatch
{
    None,
    Exact,
    SingleTypo,
}
