using System.Globalization;
using System.Text;

namespace CaptureTool.Domain.Analysis;

public static class CaptureMemoryTextNormalizer
{
    private const int MinimumPrefixRuneCount = 3;
    private const int MinimumSubstringRuneCount = 4;

    /// <summary>Uses the same token rules for retrieval and highlighting original UTF-16 text.</summary>
    public static CaptureTextMatch FindMatches(string text, string query, bool includePartialHighlights = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(query);
        CaptureMemoryNormalizedText normalized;
        CaptureMemoryNormalizedText normalizedQuery;
        try
        {
            normalized = Normalize(text);
            normalizedQuery = Normalize(query);
        }
        catch (ArgumentException)
        {
            return new(CaptureMemoryTokenMatch.None, []);
        }

        CaptureMemoryTokenMatch match = MatchTokens(normalizedQuery.Tokens, normalized.TokenSet, normalized.Tokens);
        if (match == CaptureMemoryTokenMatch.None && !includePartialHighlights)
        {
            return new(match, []);
        }

        // Text elements preserve combining marks, surrogate pairs and compatibility ligatures.
        // A prefix or typo highlights the actual matching word, never the query's spelling.
        var tokens = new List<(string Value, CaptureTextRange Range)>();
        var token = new StringBuilder();
        int start = 0;
        int end = 0;
        TextElementEnumerator elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            foreach (Rune rune in element.Normalize(NormalizationForm.FormKC).EnumerateRunes())
            {
                UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                bool content = Rune.IsLetterOrDigit(rune) || category is UnicodeCategory.NonSpacingMark or
                    UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
                if (content)
                {
                    if (token.Length == 0) { start = elements.ElementIndex; }
                    end = elements.ElementIndex + element.Length;
                    token.Append(Rune.ToLowerInvariant(rune).ToString());
                }
                else if (token.Length > 0)
                {
                    tokens.Add((token.ToString(), new(start, end - start)));
                    token.Clear();
                }
            }
        }
        if (token.Length > 0) { tokens.Add((token.ToString(), new(start, end - start))); }

        var ranges = new List<CaptureTextRange>();
        foreach (string queryToken in normalizedQuery.Tokens)
        {
            CaptureMemoryTokenMatch required = MatchTokens([queryToken], normalized.TokenSet, normalized.Tokens);
            if (required == CaptureMemoryTokenMatch.None) { continue; }
            foreach (var candidate in tokens)
            {
                if (MatchTokens([queryToken], new HashSet<string>([candidate.Value], StringComparer.Ordinal),
                    [candidate.Value]) == required)
                {
                    ranges.Add(candidate.Range);
                }
            }
        }

        var merged = new List<CaptureTextRange>();
        foreach (CaptureTextRange range in ranges.Distinct().OrderBy(range => range.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].Start + merged[^1].Length)
            {
                CaptureTextRange previous = merged[^1];
                merged[^1] = new(previous.Start, Math.Max(previous.Start + previous.Length, range.Start + range.Length) - previous.Start);
            }
            else { merged.Add(range); }
        }
        return new(match, merged.AsReadOnly());
    }

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
        CaptureMemoryTokenMatch weakestMatch = CaptureMemoryTokenMatch.Exact;
        foreach (string queryToken in queryTokens.Distinct(StringComparer.Ordinal))
        {
            if (fieldTokens.Contains(queryToken))
            {
                continue;
            }

            int queryRuneCount = CountRunes(queryToken);
            if (queryRuneCount >= MinimumPrefixRuneCount &&
                orderedFieldTokens.Any(candidate => candidate.StartsWith(
                    queryToken,
                    StringComparison.Ordinal)))
            {
                weakestMatch = Max(weakestMatch, CaptureMemoryTokenMatch.Prefix);
                continue;
            }

            if (queryRuneCount >= MinimumSubstringRuneCount &&
                orderedFieldTokens.Any(candidate => candidate.Contains(
                    queryToken,
                    StringComparison.Ordinal)))
            {
                weakestMatch = Max(weakestMatch, CaptureMemoryTokenMatch.Substring);
                continue;
            }

            // Typo matching is deliberately conservative: only one query term may use a
            // single insertion, deletion, substitution, or adjacent transposition, and short
            // terms never use fuzzy matching. This keeps ranking explainable and deterministic.
            if (fuzzyMatches > 0 || queryRuneCount < 5 ||
                !orderedFieldTokens.Any(candidate => IsOneEditAway(queryToken, candidate)))
            {
                return CaptureMemoryTokenMatch.None;
            }

            fuzzyMatches++;
            weakestMatch = Max(weakestMatch, CaptureMemoryTokenMatch.SingleTypo);
        }

        return weakestMatch;
    }

    public static string CreateSafeSnippet(string source, string query, int maximumLength = 1024)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(query);

        ArgumentOutOfRangeException.ThrowIfLessThan(maximumLength, 16);
        var builder = new StringBuilder(Math.Min(source.Length, maximumLength));
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
        int MaximumLength = maximumLength;
        if (collapsed.Length <= MaximumLength)
        {
            return collapsed;
        }

        int match = collapsed.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase);
        if (match < 0 && FindMatches(collapsed, query).Ranges.FirstOrDefault() is { Length: > 0 } firstMatch)
        {
            match = firstMatch.Start;
        }
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

        bool hasPrefix = start > 0;
        bool hasSuffix = start + length < collapsed.Length;
        length = Math.Min(length, MaximumLength - (hasPrefix ? 1 : 0) - (hasSuffix ? 1 : 0));
        if (length > 0 && char.IsHighSurrogate(collapsed[start + length - 1])) { length--; }
        return (hasPrefix ? "…" : string.Empty) + collapsed.Substring(start, length) + (hasSuffix ? "…" : string.Empty);
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

    private static CaptureMemoryTokenMatch Max(
        CaptureMemoryTokenMatch left,
        CaptureMemoryTokenMatch right)
    {
        return (CaptureMemoryTokenMatch)Math.Max((int)left, (int)right);
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

public sealed record CaptureMemoryNormalizedText(string Value, string[] Tokens)
{
    public IReadOnlySet<string> TokenSet { get; } = Tokens.ToHashSet(StringComparer.Ordinal);
}

public enum CaptureMemoryTokenMatch
{
    None,
    Exact,
    SingleTypo,
    Prefix,
    Substring,
}

public readonly record struct CaptureTextRange(int Start, int Length);

public sealed record CaptureTextMatch(CaptureMemoryTokenMatch Kind, IReadOnlyList<CaptureTextRange> Ranges)
{
    public bool IsMatch => Kind != CaptureMemoryTokenMatch.None;

    public bool IsApproximate => Kind == CaptureMemoryTokenMatch.SingleTypo;
}
