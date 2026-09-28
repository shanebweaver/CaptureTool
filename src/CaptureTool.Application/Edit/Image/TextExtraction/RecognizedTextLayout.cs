using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Analysis.TextLayout;
using System.Drawing;

namespace CaptureTool.Application.Edit.Image.TextExtraction;

public sealed class RecognizedTextLayout
{
    private const float CutoutPadding = 4;
    private const float CornerRadiusScale = 0.22f;
    private const float MinimumCornerRadius = 2;
    private const float MaximumCornerRadius = 12;
    private const float GeometryEpsilon = 0.001f;
    private const float MinimumHitTestTolerance = 6;
    private readonly IReadOnlyList<WordPlacement> _readingOrder;

    private RecognizedTextLayout(
        IReadOnlyList<RecognizedTextLayoutBlock> blocks,
        IReadOnlyList<WordPlacement> readingOrder,
        IReadOnlyList<IReadOnlyList<PointF>> cutoutContours)
    {
        Blocks = blocks;
        _readingOrder = readingOrder;
        ReadingOrder = readingOrder.Select(placement => placement.Region).ToArray();
        CutoutContours = cutoutContours;
    }

    public static RecognizedTextLayout Empty { get; } = new([], [], []);

    public IReadOnlyList<RecognizedTextLayoutBlock> Blocks { get; }

    public IReadOnlyList<RecognizedTextRegion> ReadingOrder { get; }

    public IReadOnlyList<IReadOnlyList<PointF>> CutoutContours { get; }

    public static RecognizedTextLayout Create(IReadOnlyList<RecognizedTextRegion>? regions, bool includeCutoutContours = true)
    {
        if (regions is null || regions.Count == 0)
        {
            return Empty;
        }

        RecognizedTextRegion[] validRegions = regions
            .Where(IsValidRegion)
            .ToArray();
        if (validRegions.Length == 0)
        {
            return Empty;
        }

        var paragraphs = CreateParagraphs(validRegions);
        List<RecognizedTextLayoutBlock> blocks = [];
        List<WordPlacement> readingOrder = [];
        foreach (var paragraph in paragraphs)
        {
            int blockIndex = blocks.Count;
            List<RecognizedTextLayoutLine> lines = [];
            foreach (var words in paragraph)
            {
                int lineIndex = lines.Count;
                lines.Add(new(lineIndex, Union(words.Select(word => word.Bounds)), words));
                readingOrder.AddRange(words.Select(word => new WordPlacement(word, blockIndex, lineIndex)));
            }
            blocks.Add(new(blockIndex, Union(lines.Select(line => line.Bounds)), lines));
        }
        return new RecognizedTextLayout(
            blocks,
            readingOrder,
            includeCutoutContours ? CreateUnionContours(CreateCutoutRectangles(blocks)) : []);
    }

    public static IReadOnlyList<IReadOnlyList<PointF>> CreateUnionContours(
        IEnumerable<RectangleF> bounds)
    {
        RectangleF[] rectangles = bounds
            .Where(rectangle => rectangle.Width > 0 && rectangle.Height > 0)
            .ToArray();
        if (rectangles.Length == 0)
        {
            return [];
        }

        float[] horizontalCoordinates = rectangles
            .SelectMany(rectangle => new[] { rectangle.Left, rectangle.Right })
            .Distinct()
            .Order()
            .ToArray();
        float[] verticalCoordinates = rectangles
            .SelectMany(rectangle => new[] { rectangle.Top, rectangle.Bottom })
            .Distinct()
            .Order()
            .ToArray();

        var horizontalIndexes = horizontalCoordinates
            .Select((coordinate, index) => (coordinate, index))
            .ToDictionary(item => item.coordinate, item => item.index);
        var verticalIndexes = verticalCoordinates
            .Select((coordinate, index) => (coordinate, index))
            .ToDictionary(item => item.coordinate, item => item.index);
        int[,] coverageChanges = new int[verticalCoordinates.Length, horizontalCoordinates.Length];
        foreach (RectangleF rectangle in rectangles)
        {
            int left = horizontalIndexes[rectangle.Left];
            int right = horizontalIndexes[rectangle.Right];
            int top = verticalIndexes[rectangle.Top];
            int bottom = verticalIndexes[rectangle.Bottom];
            coverageChanges[top, left]++;
            coverageChanges[top, right]--;
            coverageChanges[bottom, left]--;
            coverageChanges[bottom, right]++;
        }

        bool[,] occupiedCells = new bool[verticalCoordinates.Length - 1, horizontalCoordinates.Length - 1];
        for (int y = 0; y < verticalCoordinates.Length; y++)
        {
            for (int x = 0; x < horizontalCoordinates.Length; x++)
            {
                int coverage = coverageChanges[y, x];
                if (y > 0)
                {
                    coverage += coverageChanges[y - 1, x];
                }

                if (x > 0)
                {
                    coverage += coverageChanges[y, x - 1];
                }

                if (x > 0 && y > 0)
                {
                    coverage -= coverageChanges[y - 1, x - 1];
                }

                coverageChanges[y, x] = coverage;
                if (x < horizontalCoordinates.Length - 1 && y < verticalCoordinates.Length - 1)
                {
                    occupiedCells[y, x] = coverage > 0;
                }
            }
        }

        HashSet<GridEdge> boundaryEdges = CreateBoundaryEdges(occupiedCells);
        return TraceContours(boundaryEdges, horizontalCoordinates, verticalCoordinates);
    }

    public static IReadOnlyList<RecognizedTextRoundedCorner> CreateRoundedContour(
        IReadOnlyList<PointF>? contour)
    {
        PointF[] points = NormalizeContour(contour);
        if (points.Length < 3)
        {
            return [];
        }

        double signedArea = GetSignedArea(points);
        List<RecognizedTextRoundedCorner> corners = new(points.Length);
        for (int index = 0; index < points.Length; index++)
        {
            PointF previous = points[(index + points.Length - 1) % points.Length];
            PointF corner = points[index];
            PointF next = points[(index + 1) % points.Length];

            float incomingLength = GetDistance(previous, corner);
            float outgoingLength = GetDistance(corner, next);
            if (incomingLength <= GeometryEpsilon || outgoingLength <= GeometryEpsilon)
            {
                corners.Add(CreateSharpCorner(corner));
                continue;
            }

            float incomingX = (corner.X - previous.X) / incomingLength;
            float incomingY = (corner.Y - previous.Y) / incomingLength;
            float outgoingX = (next.X - corner.X) / outgoingLength;
            float outgoingY = (next.Y - corner.Y) / outgoingLength;
            float turnCrossProduct = (incomingX * outgoingY) - (incomingY * outgoingX);
            if (Math.Abs(turnCrossProduct) <= GeometryEpsilon)
            {
                corners.Add(CreateSharpCorner(corner));
                continue;
            }

            float towardPreviousX = -incomingX;
            float towardPreviousY = -incomingY;
            float rayDotProduct = Math.Clamp(
                (towardPreviousX * outgoingX) + (towardPreviousY * outgoingY),
                -1,
                1);
            double cornerAngle = Math.Acos(rayDotProduct);
            double tangentFactor = Math.Tan(cornerAngle / 2);
            if (!double.IsFinite(tangentFactor) || tangentFactor <= GeometryEpsilon)
            {
                corners.Add(CreateSharpCorner(corner));
                continue;
            }

            float shortestEdge = Math.Min(incomingLength, outgoingLength);
            float preferredRadius = Math.Clamp(
                shortestEdge * CornerRadiusScale,
                MinimumCornerRadius,
                MaximumCornerRadius);
            float tangentDistance = (float)(preferredRadius / tangentFactor);
            tangentDistance = Math.Min(tangentDistance, shortestEdge / 2);
            float effectiveRadius = (float)(tangentDistance * tangentFactor);

            PointF entryPoint = new(
                corner.X - (incomingX * tangentDistance),
                corner.Y - (incomingY * tangentDistance));
            PointF exitPoint = new(
                corner.X + (outgoingX * tangentDistance),
                corner.Y + (outgoingY * tangentDistance));
            bool isConcave = Math.Abs(signedArea) > GeometryEpsilon &&
                Math.Sign(turnCrossProduct) != Math.Sign(signedArea);

            corners.Add(new RecognizedTextRoundedCorner(
                entryPoint,
                corner,
                exitPoint,
                effectiveRadius,
                isConcave,
                turnCrossProduct > 0));
        }

        return corners;
    }

    public int? HitTest(PointF point, bool allowNearest = false)
    {
        for (int index = 0; index < _readingOrder.Count; index++)
        {
            if (_readingOrder[index].Region.Bounds.Contains(point))
            {
                return index;
            }
        }

        if (_readingOrder.Count == 0)
        {
            return null;
        }

        int closestIndex = -1;
        float closestDistanceSquared = float.MaxValue;
        for (int index = 0; index < _readingOrder.Count; index++)
        {
            RectangleF bounds = _readingOrder[index].Region.Bounds;
            float distanceSquared = DistanceSquared(point, bounds);
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestIndex = index;
            }
        }

        if (allowNearest)
        {
            return closestIndex;
        }

        float tolerance = Math.Max(
            MinimumHitTestTolerance,
            _readingOrder[closestIndex].Region.Bounds.Height * 0.6f);
        return closestDistanceSquared <= tolerance * tolerance
            ? closestIndex
            : null;
    }

    public RecognizedTextPosition? HitTestPosition(PointF point, bool allowNearest = false)
    {
        int? regionIndex = HitTest(point, allowNearest);
        if (!regionIndex.HasValue)
        {
            return null;
        }

        RecognizedTextRegion region = _readingOrder[regionIndex.Value].Region;
        float horizontalPosition = Math.Clamp(
            (point.X - region.Bounds.Left) / region.Bounds.Width,
            0,
            1);
        int characterIndex = (int)Math.Round(
            horizontalPosition * region.Text.Length,
            MidpointRounding.AwayFromZero);
        return new RecognizedTextPosition(regionIndex.Value, characterIndex);
    }

    public RecognizedTextSelection Select(int anchorIndex, int focusIndex)
    {
        if (_readingOrder.Count == 0 ||
            anchorIndex < 0 || anchorIndex >= _readingOrder.Count ||
            focusIndex < 0 || focusIndex >= _readingOrder.Count)
        {
            return RecognizedTextSelection.Empty;
        }

        int startIndex = Math.Min(anchorIndex, focusIndex);
        int endIndex = Math.Max(anchorIndex, focusIndex);
        return Select(
            new RecognizedTextPosition(startIndex, 0),
            new RecognizedTextPosition(endIndex, _readingOrder[endIndex].Region.Text.Length));
    }

    public RecognizedTextSelection Select(
        RecognizedTextPosition anchor,
        RecognizedTextPosition focus)
    {
        if (!IsValidPosition(anchor) || !IsValidPosition(focus))
        {
            return RecognizedTextSelection.Empty;
        }

        RecognizedTextPosition start = ComparePositions(anchor, focus) <= 0 ? anchor : focus;
        RecognizedTextPosition end = ComparePositions(anchor, focus) <= 0 ? focus : anchor;
        if (start == end)
        {
            return RecognizedTextSelection.Empty;
        }

        List<SelectedWord> selectedWords = [];
        for (int regionIndex = start.RegionIndex; regionIndex <= end.RegionIndex; regionIndex++)
        {
            WordPlacement placement = _readingOrder[regionIndex];
            int startCharacter = regionIndex == start.RegionIndex ? start.CharacterIndex : 0;
            int endCharacter = regionIndex == end.RegionIndex
                ? end.CharacterIndex
                : placement.Region.Text.Length;
            if (endCharacter <= startCharacter)
            {
                continue;
            }

            RectangleF bounds = GetCharacterBounds(
                placement.Region,
                startCharacter,
                endCharacter);
            selectedWords.Add(new SelectedWord(
                placement,
                placement.Region.Text[startCharacter..endCharacter],
                bounds));
        }

        if (selectedWords.Count == 0)
        {
            return RecognizedTextSelection.Empty;
        }

        List<RectangleF> highlightBounds = [];
        foreach (IGrouping<(int BlockIndex, int LineIndex), SelectedWord> line in selectedWords
            .GroupBy(word => (word.Placement.BlockIndex, word.Placement.LineIndex)))
        {
            highlightBounds.Add(Union(line.Select(word => word.Bounds)));
        }

        return new RecognizedTextSelection(
            CreateSelectedText(selectedWords),
            selectedWords.Select(word => word.Placement.Region).ToArray(),
            highlightBounds);
    }

    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<RecognizedTextRegion>>> CreateParagraphs(RecognizedTextRegion[] regions)
    {
        // Pane overlays already carry the shared layout. Keep those exact paragraph/line boundaries
        // so a clicked passage, drag selection, and copied text all describe the same words.
        if (regions.All(region => region.ParagraphIndex >= 0 && region.LineIndex >= 0))
            return regions.GroupBy(region => region.ParagraphIndex).Select(paragraph =>
                (IReadOnlyList<IReadOnlyList<RecognizedTextRegion>>)paragraph.GroupBy(region => region.LineIndex)
                    .Select(line => (IReadOnlyList<RecognizedTextRegion>)line.ToArray()).ToArray()).ToArray();

        double left = regions.Min(region => (double)region.Bounds.Left);
        double top = regions.Min(region => (double)region.Bounds.Top);
        double width = regions.Max(region => (double)region.Bounds.Right) - left;
        double height = regions.Max(region => (double)region.Bounds.Bottom) - top;
        var words = regions.Select(region =>
        {
            double x = (region.Bounds.Left - left) / width, y = (region.Bounds.Top - top) / height;
            return new RecognizedText(region.Text,
                new(x, y, Math.Min(1 - x, region.Bounds.Width / width), Math.Min(1 - y, region.Bounds.Height / height)),
                lineIndex: region.LineIndex >= 0 ? region.LineIndex : null, wordIndex: region.WordIndex >= 0 ? region.WordIndex : null);
        }).ToArray();
        return RecognizedTextGrouping.Create(words).Select(paragraph =>
            (IReadOnlyList<IReadOnlyList<RecognizedTextRegion>>)paragraph.Lines.Select(line =>
                (IReadOnlyList<RecognizedTextRegion>)line.WordIndices.Select(index => regions[index]).ToArray()).ToArray()).ToArray();
    }
    private static string CreateSelectedText(IReadOnlyList<SelectedWord> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var text = new System.Text.StringBuilder(words[0].Text);
        for (int index = 1; index < words.Count; index++)
        {
            SelectedWord previous = words[index - 1];
            SelectedWord current = words[index];
            if (previous.Placement.BlockIndex != current.Placement.BlockIndex)
            {
                text.AppendLine();
                text.AppendLine();
            }
            else if (previous.Placement.LineIndex != current.Placement.LineIndex)
            {
                text.AppendLine();
            }
            else
            {
                text.Append(' ');
            }

            text.Append(current.Text);
        }

        return text.ToString();
    }

    private bool IsValidPosition(RecognizedTextPosition position)
    {
        return position.RegionIndex >= 0 &&
            position.RegionIndex < _readingOrder.Count &&
            position.CharacterIndex >= 0 &&
            position.CharacterIndex <= _readingOrder[position.RegionIndex].Region.Text.Length;
    }

    private static int ComparePositions(
        RecognizedTextPosition first,
        RecognizedTextPosition second)
    {
        int regionComparison = first.RegionIndex.CompareTo(second.RegionIndex);
        return regionComparison != 0
            ? regionComparison
            : first.CharacterIndex.CompareTo(second.CharacterIndex);
    }

    private static RectangleF GetCharacterBounds(
        RecognizedTextRegion region,
        int startCharacter,
        int endCharacter)
    {
        float characterWidth = region.Bounds.Width / region.Text.Length;
        return new RectangleF(
            region.Bounds.Left + (characterWidth * startCharacter),
            region.Bounds.Top,
            characterWidth * (endCharacter - startCharacter),
            region.Bounds.Height);
    }

    private static bool IsValidRegion(RecognizedTextRegion region)
    {
        return !string.IsNullOrWhiteSpace(region.Text) &&
            region.Bounds.Width > 0 &&
            region.Bounds.Height > 0;
    }

    private static RectangleF Union(IEnumerable<RectangleF> bounds)
    {
        using IEnumerator<RectangleF> enumerator = bounds.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return RectangleF.Empty;
        }

        RectangleF result = enumerator.Current;
        while (enumerator.MoveNext())
        {
            result = RectangleF.Union(result, enumerator.Current);
        }

        return result;
    }

    private static IReadOnlyList<RectangleF> CreateCutoutRectangles(
        IReadOnlyList<RecognizedTextLayoutBlock> blocks)
    {
        List<RectangleF> rectangles = [];
        foreach (RecognizedTextLayoutBlock block in blocks)
        {
            RectangleF[] lineBounds = block.Lines
                .Select(line => line.Bounds)
                .ToArray();
            NormalizeNearlyAlignedEdges(lineBounds);
            for (int index = 0; index < lineBounds.Length; index++)
            {
                lineBounds[index] = Inflate(lineBounds[index], CutoutPadding);
            }

            rectangles.AddRange(lineBounds);

            for (int index = 1; index < lineBounds.Length; index++)
            {
                RectangleF previous = lineBounds[index - 1];
                RectangleF current = lineBounds[index];
                float connectorTop = previous.Bottom;
                float connectorBottom = current.Top;
                float connectorLeft = Math.Max(previous.Left, current.Left);
                float connectorRight = Math.Min(previous.Right, current.Right);
                if (connectorBottom > connectorTop && connectorRight > connectorLeft)
                {
                    rectangles.Add(RectangleF.FromLTRB(
                        connectorLeft,
                        connectorTop,
                        connectorRight,
                        connectorBottom));
                }
            }
        }

        return rectangles;
    }

    private static RectangleF Inflate(RectangleF bounds, float padding)
    {
        return RectangleF.FromLTRB(
            bounds.Left - padding,
            bounds.Top - padding,
            bounds.Right + padding,
            bounds.Bottom + padding);
    }

    private static void NormalizeNearlyAlignedEdges(RectangleF[] lineBounds)
    {
        for (int index = 1; index < lineBounds.Length; index++)
        {
            RectangleF previous = lineBounds[index - 1];
            RectangleF current = lineBounds[index];
            float tolerance = Math.Max(previous.Height, current.Height) * 0.75f;
            if (Math.Abs(previous.Left - current.Left) <= tolerance)
            {
                float commonLeft = Math.Min(previous.Left, current.Left);
                previous = RectangleF.FromLTRB(commonLeft, previous.Top, previous.Right, previous.Bottom);
                current = RectangleF.FromLTRB(commonLeft, current.Top, current.Right, current.Bottom);
            }

            if (Math.Abs(previous.Right - current.Right) <= tolerance)
            {
                float commonRight = Math.Max(previous.Right, current.Right);
                previous = RectangleF.FromLTRB(previous.Left, previous.Top, commonRight, previous.Bottom);
                current = RectangleF.FromLTRB(current.Left, current.Top, commonRight, current.Bottom);
            }

            lineBounds[index - 1] = previous;
            lineBounds[index] = current;
        }
    }

    private static HashSet<GridEdge> CreateBoundaryEdges(bool[,] occupiedCells)
    {
        var edges = new HashSet<GridEdge>();
        int rowCount = occupiedCells.GetLength(0);
        int columnCount = occupiedCells.GetLength(1);
        for (int y = 0; y < rowCount; y++)
        {
            for (int x = 0; x < columnCount; x++)
            {
                if (!occupiedCells[y, x])
                {
                    continue;
                }

                if (y == 0 || !occupiedCells[y - 1, x])
                {
                    edges.Add(new GridEdge(new GridPoint(x, y), new GridPoint(x + 1, y)));
                }

                if (x == columnCount - 1 || !occupiedCells[y, x + 1])
                {
                    edges.Add(new GridEdge(new GridPoint(x + 1, y), new GridPoint(x + 1, y + 1)));
                }

                if (y == rowCount - 1 || !occupiedCells[y + 1, x])
                {
                    edges.Add(new GridEdge(new GridPoint(x + 1, y + 1), new GridPoint(x, y + 1)));
                }

                if (x == 0 || !occupiedCells[y, x - 1])
                {
                    edges.Add(new GridEdge(new GridPoint(x, y + 1), new GridPoint(x, y)));
                }
            }
        }

        return edges;
    }

    private static IReadOnlyList<IReadOnlyList<PointF>> TraceContours(
        HashSet<GridEdge> unusedEdges,
        IReadOnlyList<float> horizontalCoordinates,
        IReadOnlyList<float> verticalCoordinates)
    {
        Dictionary<GridPoint, List<GridEdge>> outgoingEdges = unusedEdges
            .GroupBy(edge => edge.Start)
            .ToDictionary(group => group.Key, group => group.ToList());
        List<IReadOnlyList<PointF>> contours = [];
        while (unusedEdges.Count > 0)
        {
            GridEdge firstEdge = unusedEdges.First();
            List<GridPoint> contour = [firstEdge.Start];
            GridEdge currentEdge = firstEdge;
            do
            {
                unusedEdges.Remove(currentEdge);
                AddContourPoint(contour, currentEdge.End);
                if (currentEdge.End == firstEdge.Start)
                {
                    break;
                }

                currentEdge = SelectNextEdge(currentEdge, outgoingEdges[currentEdge.End], unusedEdges);
            }
            while (true);

            if (contour.Count > 1 && contour[^1] == contour[0])
            {
                contour.RemoveAt(contour.Count - 1);
            }

            contours.Add(contour
                .Select(point => new PointF(
                    horizontalCoordinates[point.X],
                    verticalCoordinates[point.Y]))
                .ToArray());
        }

        return contours;
    }

    private static GridEdge SelectNextEdge(
        GridEdge current,
        IEnumerable<GridEdge> candidates,
        IReadOnlySet<GridEdge> unusedEdges)
    {
        int currentDirection = GetDirection(current);
        return candidates
            .Where(unusedEdges.Contains)
            .OrderBy(candidate => GetTurnPriority(
                (GetDirection(candidate) - currentDirection + 4) % 4))
            .First();
    }

    private static int GetDirection(GridEdge edge)
    {
        if (edge.End.X > edge.Start.X)
        {
            return 0;
        }

        if (edge.End.Y > edge.Start.Y)
        {
            return 1;
        }

        return edge.End.X < edge.Start.X ? 2 : 3;
    }

    private static int GetTurnPriority(int turn)
    {
        return turn switch
        {
            1 => 0,
            0 => 1,
            3 => 2,
            _ => 3
        };
    }

    private static void AddContourPoint(List<GridPoint> contour, GridPoint point)
    {
        if (contour.Count < 2)
        {
            contour.Add(point);
            return;
        }

        GridPoint previous = contour[^1];
        GridPoint beforePrevious = contour[^2];
        bool isCollinear =
            beforePrevious.X == previous.X && previous.X == point.X ||
            beforePrevious.Y == previous.Y && previous.Y == point.Y;
        if (isCollinear)
        {
            contour[^1] = point;
        }
        else
        {
            contour.Add(point);
        }
    }

    private static float DistanceSquared(PointF point, RectangleF bounds)
    {
        float deltaX = Math.Max(bounds.Left - point.X, Math.Max(0, point.X - bounds.Right));
        float deltaY = Math.Max(bounds.Top - point.Y, Math.Max(0, point.Y - bounds.Bottom));
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static PointF[] NormalizeContour(IReadOnlyList<PointF>? contour)
    {
        if (contour is null || contour.Count == 0)
        {
            return [];
        }

        List<PointF> points = new(contour.Count);
        foreach (PointF point in contour)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
            {
                continue;
            }

            if (points.Count == 0 || GetDistance(points[^1], point) > GeometryEpsilon)
            {
                points.Add(point);
            }
        }

        if (points.Count > 1 && GetDistance(points[0], points[^1]) <= GeometryEpsilon)
        {
            points.RemoveAt(points.Count - 1);
        }

        return [.. points];
    }

    private static double GetSignedArea(IReadOnlyList<PointF> contour)
    {
        double twiceArea = 0;
        for (int index = 0; index < contour.Count; index++)
        {
            PointF current = contour[index];
            PointF next = contour[(index + 1) % contour.Count];
            twiceArea += (current.X * next.Y) - (next.X * current.Y);
        }

        return twiceArea / 2;
    }

    private static float GetDistance(PointF first, PointF second)
    {
        float deltaX = second.X - first.X;
        float deltaY = second.Y - first.Y;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static RecognizedTextRoundedCorner CreateSharpCorner(PointF corner)
    {
        return new RecognizedTextRoundedCorner(
            corner,
            corner,
            corner,
            0,
            false,
            false);
    }

    private sealed record WordPlacement(
        RecognizedTextRegion Region,
        int BlockIndex,
        int LineIndex);

    private sealed record SelectedWord(
        WordPlacement Placement,
        string Text,
        RectangleF Bounds);

    private readonly record struct GridPoint(int X, int Y);

    private readonly record struct GridEdge(GridPoint Start, GridPoint End);
}

public readonly record struct RecognizedTextPosition(
    int RegionIndex,
    int CharacterIndex);

public sealed record RecognizedTextLayoutBlock(
    int Index,
    RectangleF Bounds,
    IReadOnlyList<RecognizedTextLayoutLine> Lines);

public sealed record RecognizedTextLayoutLine(
    int Index,
    RectangleF Bounds,
    IReadOnlyList<RecognizedTextRegion> Regions);

public sealed record RecognizedTextSelection(
    string Text,
    IReadOnlyList<RecognizedTextRegion> Regions,
    IReadOnlyList<RectangleF> HighlightBounds)
{
    public static RecognizedTextSelection Empty { get; } = new(string.Empty, [], []);
}

public readonly record struct RecognizedTextRoundedCorner(
    PointF EntryPoint,
    PointF CornerPoint,
    PointF ExitPoint,
    float Radius,
    bool IsConcave,
    bool SweepsClockwise);
