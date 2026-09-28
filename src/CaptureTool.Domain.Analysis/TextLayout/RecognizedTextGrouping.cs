using CaptureTool.Domain.Analysis.Payloads;
using System.Globalization;

namespace CaptureTool.Domain.Analysis.TextLayout;

public sealed record RecognizedTextLine(IReadOnlyList<int> WordIndices, string Text, NormalizedBounds? Bounds)
{
    internal double CharacterWidth { get; init; }
    internal double TextHeight { get; init; }
}
public sealed record RecognizedTextParagraph(IReadOnlyList<RecognizedTextLine> Lines, NormalizedBounds? Bounds)
{
    public int FirstIndex => Lines.SelectMany(line => line.WordIndices).Min();
    public string Text => string.Join(Environment.NewLine, Lines.Select(line => line.Text));
}

/// <summary>
/// Builds display layout while retaining indices into immutable OCR evidence. Horizontal distances
/// use character widths and vertical distances use text heights, so each axis can be scaled independently.
/// </summary>
public static class RecognizedTextGrouping
{
    public static IReadOnlyList<RecognizedTextParagraph> Create(IReadOnlyList<RecognizedText> words)
    {
        List<RecognizedTextParagraph> result = [];
        int start = 0;
        while (start < words.Count)
        {
            if (!CanGroup(words[start]))
            {
                var line = Line([start], words);
                result.Add(new([line], line.Bounds));
                start++;
                continue;
            }
            int end = start + 1;
            while (end < words.Count && CanGroup(words[end]) && words[end].Timestamp == words[start].Timestamp) end++;
            GroupRange(start, end, words, result);
            start = end;
        }
        return result;
    }

    private static void GroupRange(int start, int end, IReadOnlyList<RecognizedText> words,
        List<RecognizedTextParagraph> result)
    {
        List<RecognizedTextLine> lines = [];
        var order = Enumerable.Range(start, end - start).ToArray();
        if (order.All(index => words[index].LineIndex != null))
            order = order.OrderBy(index => words[index].LineIndex).ThenBy(index => words[index].WordIndex ?? index).ToArray();
        var ranks = order.Select((index, rank) => (index, rank)).ToDictionary(item => item.index, item => item.rank);
        // Do not sort words by X: the provider's sequence also carries bidirectional reading order.
        List<int> current = [];
        foreach (int i in order)
        {
            if (current.Count > 0 && !SameLine(current, i, words))
            {
                lines.Add(Line(current.ToArray(), words));
                current.Clear();
            }
            current.Add(i);
        }
        if (current.Count > 0) lines.Add(Line(current.ToArray(), words));

        // Track separate columns even when OCR returns alternating left/right rows. A flow is
        // only a candidate text block; paragraph boundaries are decided using its local spacing.
        List<List<RecognizedTextLine>> flows = [];
        List<List<RecognizedTextLine>> active = [];
        foreach (var line in lines.OrderBy(line => line.Bounds!.Y).ThenBy(line => line.WordIndices[0]))
        {
            var b = line.Bounds!;
            active.RemoveAll(flow => b.Y - Bottom(flow[^1].Bounds!) > Math.Min(b.Height, flow[^1].Bounds!.Height) * 3);
            var candidates = active.Where(flow => Related(flow[^1], line)).ToArray();
            List<RecognizedTextLine>? match = candidates
                .OrderBy(flow => Math.Abs(b.Y - Bottom(flow[^1].Bounds!)))
                .ThenBy(flow => EdgeDistance(flow[^1].Bounds!, b)).FirstOrDefault();

            // A spanning heading must not connect two columns or allow either column to jump over it.
            var covered = active.Where(flow => b.Y >= Bottom(flow[^1].Bounds!) - b.Height * .2 &&
                Overlap(flow[^1].Bounds!, b) >= flow[^1].Bounds!.Width * .8).ToArray();
            bool spansColumns = covered.Any(flow => covered.Any(other => other != flow &&
                Overlap(flow[^1].Bounds!, other[^1].Bounds!) <= 0));
            if (spansColumns)
            {
                foreach (var flow in covered) active.Remove(flow);
                match = null;
            }
            else
            {
                // Also stop a body flow at a heading with a different font size.
                foreach (var flow in covered.Where(flow => flow != match)) active.Remove(flow);
            }
            if (match == null)
            {
                match = [];
                flows.Add(match);
                active.Add(match);
                // Dense UI or malformed OCR can contain thousands of unrelated items on one row.
                // Bound neighbor search; overflow stays separate and no evidence is discarded.
                if (active.Count > 64) active.RemoveAt(0);
            }
            match.Add(line);
        }

        List<RecognizedTextParagraph> paragraphs = [];
        foreach (var flow in flows.OrderBy(flow => flow.SelectMany(line => line.WordIndices).Min(index => ranks[index])))
        {
            double height = Median(flow.Select(line => line.TextHeight));
            var gaps = flow.Zip(flow.Skip(1), (a, b) => Math.Max(0, b.Bounds!.Y - Bottom(a.Bounds!))).Order().ToArray();
            // The lower half excludes paragraph gaps. Two isolated lines cannot establish a
            // spacing pattern, so use the conservative fallback until there are three lines.
            double typicalGap = gaps.Length >= 2 ? Median(gaps.Take((gaps.Length + 1) / 2)) : height * .35;
            double gapLimit = Math.Max(height * .65, typicalGap * 1.65 + height * .1);
            List<RecognizedTextLine> paragraph = [];
            foreach (var line in flow)
            {
                if (paragraph.Count > 0 && !SameParagraph(paragraph, line, gapLimit))
                {
                    paragraphs.Add(Paragraph(paragraph));
                    paragraph = [];
                }
                paragraph.Add(line);
            }
            paragraphs.Add(Paragraph(paragraph));
        }
        // Preserve the provider's block order, including RTL columns, while keeping each column's
        // lines together. Never reorder or rewrite the underlying evidence collection.
        result.AddRange(paragraphs);
    }

    private static bool SameLine(List<int> line, int index, IReadOnlyList<RecognizedText> words)
    {
        var previous = words[line[^1]];
        var current = words[index];
        if (previous.LineIndex != current.LineIndex && (previous.LineIndex != null || current.LineIndex != null)) return false;
        var a = previous.Bounds!;
        var b = current.Bounds!;
        // A bounded local sample follows gradual size/baseline changes and keeps very long OCR
        // lines from requiring quadratic work on the UI thread.
        var sample = line.TakeLast(16).Select(i => words[i]).ToArray();
        double characterWidth = Median(sample.Select(CharacterWidth).Append(CharacterWidth(current)));
        double gap = Math.Max(a.X, b.X) - Math.Min(Right(a), Right(b));
        // Provider lines are useful hints, but can span unrelated columns in screenshots.
        if (gap > characterWidth * (current.LineIndex != null ? 5 : 3)) return false;
        double height = Median(sample.Select(word => word.Bounds!.Height));
        double baseline = Median(sample.Select(word => Bottom(word.Bounds!)));
        if (current.LineIndex != null)
            return Math.Abs(Bottom(b) - baseline) <= Math.Max(height, b.Height) * .8;
        double ratio = Math.Max(height, b.Height) / Math.Min(height, b.Height);
        return ratio <= 1.8 && Math.Abs(Bottom(b) - baseline) <= height * .45;
    }

    private static bool Related(RecognizedTextLine previous, RecognizedTextLine current)
    {
        var a = previous.Bounds!;
        var b = current.Bounds!;
        double height = Math.Max(previous.TextHeight, current.TextHeight);
        if (height > Math.Min(previous.TextHeight, current.TextHeight) * 1.4) return false;
        double gap = b.Y - Bottom(a);
        if (gap < -height * .25 || gap > Math.Min(previous.TextHeight, current.TextHeight) * 3) return false;
        double tolerance = Math.Min(previous.CharacterWidth, current.CharacterWidth) * 2;
        return Overlap(a, b) > 0 && (EdgeDistance(a, b) <= tolerance ||
            Overlap(a, b) >= Math.Min(a.Width, b.Width) * .8 && Math.Abs(a.X - b.X) <= tolerance * 2);
    }

    private static bool SameParagraph(List<RecognizedTextLine> paragraph, RecognizedTextLine current,
        double gapLimit)
    {
        var previous = paragraph[^1];
        var a = previous.Bounds!;
        var b = current.Bounds!;
        if (b.Y - Bottom(a) > gapLimit || ListPrefixLength(current.Text) > 0) return false;
        if (Math.Max(previous.TextHeight, current.TextHeight) > Math.Min(previous.TextHeight, current.TextHeight) * 1.2)
            return false;
        double width = Math.Min(previous.CharacterWidth, current.CharacterWidth);
        var first = paragraph[0];
        bool rtl = IsRightToLeft(first.Text);
        double leadingA = rtl ? -Right(a) : a.X, leadingB = rtl ? -Right(b) : b.X;
        double trailingDifference = rtl ? Math.Abs(a.X - b.X) : Math.Abs(Right(a) - Right(b));
        double indent = leadingB - leadingA;
        int prefix = ListPrefixLength(first.Text);
        if (prefix > 0)
        {
            double bodyStart = (rtl ? -Right(first.Bounds!) : first.Bounds!.X) + prefix * first.CharacterWidth;
            // Wrapped list text aligns with the body, while the next marker starts a new item.
            return Math.Abs(leadingB - bodyStart) <= width * 2 || Math.Abs(indent) <= width;
        }
        if (indent > width * 2) return false; // First-line indent starts a paragraph.
        if (indent < -width * 2)
            return paragraph.Count == 1 && -indent <= width * 6 &&
                (trailingDifference <= width * 2 || a.Width >= b.Width * .8);
        return true;
    }

    private static bool IsRightToLeft(string text)
    {
        foreach (char character in text)
            if (char.IsLetter(character))
                return character is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFDFF' or >= '\uFE70' and <= '\uFEFF';
        return false;
    }

    private static int ListPrefixLength(string text)
    {
        int start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
        if (start == text.Length) return 0;
        int end = start;
        if (text[start] is '•' or '◦' or '▪' or '‣' or '-' or '*') end++;
        else
        {
            if (char.IsDigit(text[end]))
                while (end < text.Length && char.IsDigit(text[end])) end++;
            else if (char.IsLetter(text[end])) end++;
            if (end == start || end >= text.Length || text[end] is not ('.' or ')')) return 0;
            end++;
        }
        if (end >= text.Length || !char.IsWhiteSpace(text[end])) return 0;
        while (end < text.Length && char.IsWhiteSpace(text[end])) end++;
        return end;
    }

    private static bool CanGroup(RecognizedText word) => word.Bounds is { Width: > 0, Height: > 0 } && word.Text.IndexOfAny(['\r', '\n']) < 0;
    private static double CharacterWidth(RecognizedText word) => word.Bounds!.Width / Math.Max(1, new StringInfo(word.Text).LengthInTextElements);
    private static double Right(NormalizedBounds bounds) => bounds.X + bounds.Width;
    private static double Bottom(NormalizedBounds bounds) => bounds.Y + bounds.Height;
    private static double Overlap(NormalizedBounds a, NormalizedBounds b) => Math.Min(Right(a), Right(b)) - Math.Max(a.X, b.X);
    private static double EdgeDistance(NormalizedBounds a, NormalizedBounds b) => Math.Min(Math.Abs(a.X - b.X), Math.Abs(Right(a) - Right(b)));
    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted.Length % 2 == 0 ? (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2 : sorted[sorted.Length / 2];
    }
    private static RecognizedTextLine Line(IReadOnlyList<int> indices, IReadOnlyList<RecognizedText> words) =>
        new(indices, string.Join(" ", indices.Select(i => words[i].Text)), Union(indices.Select(i => words[i].Bounds)))
        {
            CharacterWidth = Median(indices.Select(i => words[i].Bounds == null ? 0 : CharacterWidth(words[i]))),
            TextHeight = Median(indices.Select(i => words[i].Bounds?.Height ?? 0))
        };
    private static RecognizedTextParagraph Paragraph(IReadOnlyList<RecognizedTextLine> lines) => new(lines, Union(lines.Select(line => line.Bounds)));
    private static NormalizedBounds? Union(IEnumerable<NormalizedBounds?> source)
    {
        var bounds = source.ToArray();
        if (bounds.Any(bound => bound is not { Width: > 0, Height: > 0 })) return null;
        double x = bounds.Min(bound => bound!.X), y = bounds.Min(bound => bound!.Y);
        return new(x, y, Math.Min(1 - x, bounds.Max(bound => Right(bound!)) - x), Math.Min(1 - y, bounds.Max(bound => Bottom(bound!)) - y));
    }
}
