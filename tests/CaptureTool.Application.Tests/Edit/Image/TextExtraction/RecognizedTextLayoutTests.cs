using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Edit.Image.TextExtraction;
using FluentAssertions;
using System.Drawing;

namespace CaptureTool.Application.Tests.Edit.Image.TextExtraction;

[TestClass]
public sealed class RecognizedTextLayoutTests
{
    [TestMethod]
    public void Create_WhenWordsAndLinesAreNearby_GroupsThemIntoOneParagraphCutout()
    {
        RecognizedTextRegion[] regions = [
            Word("The", 10, 10, 24, 12, 0, 0),
            Word("first", 39, 10, 32, 12, 0, 1),
            Word("line.", 76, 10, 30, 12, 0, 2),
            Word("The", 10, 27, 24, 12, 1, 0),
            Word("second", 39, 27, 45, 12, 1, 1),
            Word("line.", 89, 27, 30, 12, 1, 2)
        ];

        RecognizedTextLayout layout = RecognizedTextLayout.Create(regions);

        layout.Blocks.Should().ContainSingle();
        layout.Blocks[0].Lines.Should().HaveCount(2);
        layout.Blocks[0].Bounds.Should().Be(new RectangleF(10, 10, 109, 29));
        layout.CutoutContours.Should().ContainSingle();
        RectangleF paddedBlockBounds = RectangleF.FromLTRB(6, 6, 123, 43);
        GetArea(layout.CutoutContours[0]).Should().BeLessThan(paddedBlockBounds.Width * paddedBlockBounds.Height);
    }

    [TestMethod]
    public void Create_WhenTextIsOnOneLine_AddsFourPixelsAroundTheVisualCutout()
    {
        RecognizedTextLayout layout = RecognizedTextLayout.Create([
            Word("padded", 10, 10, 30, 12, 0, 0)
        ]);

        layout.Blocks.Should().ContainSingle();
        layout.Blocks[0].Bounds.Should().Be(new RectangleF(10, 10, 30, 12));
        layout.CutoutContours.Should().ContainSingle();
        GetBounds(layout.CutoutContours[0]).Should().Be(new RectangleF(6, 6, 38, 20));
    }

    [TestMethod]
    public void Create_WhenWordsOnOneOcrLineHaveALargeGap_SplitsUnrelatedCutouts()
    {
        RecognizedTextRegion[] regions = [
            Word("left", 10, 10, 25, 10, 0, 0),
            Word("column", 40, 10, 42, 10, 0, 1),
            Word("right", 240, 10, 30, 10, 0, 2),
            Word("column", 275, 10, 42, 10, 0, 3)
        ];

        RecognizedTextLayout layout = RecognizedTextLayout.Create(regions);

        layout.Blocks.Should().HaveCount(2);
        layout.Blocks.Select(block => block.Bounds).Should().Equal(
            new RectangleF(10, 10, 72, 10),
            new RectangleF(240, 10, 77, 10));
    }

    [TestMethod]
    public void Create_WhenSuccessiveLinesAreInDifferentColumns_DoesNotJoinCutouts()
    {
        RecognizedTextRegion[] regions = [
            Word("left", 10, 10, 30, 12, 0, 0),
            Word("right", 210, 26, 35, 12, 1, 0)
        ];

        RecognizedTextLayout layout = RecognizedTextLayout.Create(regions);

        layout.Blocks.Should().HaveCount(2);
    }

    [TestMethod]
    public void Select_AcrossLines_ReturnsReadingOrderTextAndLineHighlights()
    {
        RecognizedTextLayout layout = RecognizedTextLayout.Create([
            Word("zero", 10, 10, 25, 10, 0, 0),
            Word("one", 40, 10, 20, 10, 0, 1),
            Word("two", 10, 25, 20, 10, 1, 0),
            Word("three", 35, 25, 30, 10, 1, 1)
        ]);

        RecognizedTextSelection selection = layout.Select(1, 3);

        selection.Text.Should().Be("one" + Environment.NewLine + "two three");
        selection.Regions.Select(region => region.Text).Should().Equal("one", "two", "three");
        selection.HighlightBounds.Should().Equal(
            new RectangleF(40, 10, 20, 10),
            new RectangleF(10, 25, 55, 10));
    }

    [TestMethod]
    public void Select_WhenDraggingBackwards_NormalizesTheRange()
    {
        RecognizedTextLayout layout = RecognizedTextLayout.Create([
            Word("zero", 10, 10, 25, 10, 0, 0),
            Word("one", 40, 10, 20, 10, 0, 1),
            Word("two", 65, 10, 20, 10, 0, 2)
        ]);

        RecognizedTextSelection selection = layout.Select(2, 0);

        selection.Text.Should().Be("zero one two");
        selection.HighlightBounds.Should().ContainSingle()
            .Which.Should().Be(new RectangleF(10, 10, 75, 10));
    }

    [TestMethod]
    public void Select_WithinAWord_UsesCharacterLevelTextAndHighlightBounds()
    {
        RecognizedTextLayout layout = RecognizedTextLayout.Create([
            Word("hello", 10, 10, 50, 12, 0, 0)
        ]);

        RecognizedTextSelection selection = layout.Select(
            new RecognizedTextPosition(0, 1),
            new RecognizedTextPosition(0, 4));

        selection.Text.Should().Be("ell");
        selection.HighlightBounds.Should().ContainSingle()
            .Which.Should().Be(new RectangleF(20, 10, 30, 12));
    }

    [TestMethod]
    public void HitTest_WhenPointerIsJustOutsideAWord_UsesTextSelectionTolerance()
    {
        RecognizedTextLayout layout = RecognizedTextLayout.Create([
            Word("word", 10, 10, 30, 12, 0, 0)
        ]);

        layout.HitTest(new PointF(43, 16)).Should().Be(0);
        layout.HitTest(new PointF(100, 100)).Should().BeNull();
        layout.HitTest(new PointF(100, 100), allowNearest: true).Should().Be(0);
    }

    [TestMethod]
    public void CreateUnionContours_WhenRectanglesOverlap_ProducesOneUnionWithoutParityOverlap()
    {
        IReadOnlyList<IReadOnlyList<PointF>> contours = RecognizedTextLayout.CreateUnionContours([
            new RectangleF(0, 0, 10, 10),
            new RectangleF(5, 5, 10, 10)
        ]);

        contours.Should().ContainSingle();
        GetArea(contours[0]).Should().BeApproximately(175, 0.001f);
    }

    [TestMethod]
    public void CreateUnionContours_WhenRectanglesShareAnEdge_MergesThemIntoOneContour()
    {
        IReadOnlyList<IReadOnlyList<PointF>> contours = RecognizedTextLayout.CreateUnionContours([
            new RectangleF(0, 0, 10, 10),
            new RectangleF(10, 0, 10, 10)
        ]);

        contours.Should().ContainSingle();
        GetArea(contours[0]).Should().BeApproximately(200, 0.001f);
    }

    [TestMethod]
    public void CreateUnionContours_WhenRectanglesAreDisjoint_KeepsSeparateContours()
    {
        IReadOnlyList<IReadOnlyList<PointF>> contours = RecognizedTextLayout.CreateUnionContours([
            new RectangleF(0, 0, 10, 10),
            new RectangleF(20, 20, 10, 10)
        ]);

        contours.Should().HaveCount(2);
        contours.Sum(GetArea).Should().BeApproximately(200, 0.001f);
    }

    [TestMethod]
    public void CreateUnionContours_WhenRectanglesOnlyTouchAtACorner_DoesNotCreateAParityCrossing()
    {
        IReadOnlyList<IReadOnlyList<PointF>> contours = RecognizedTextLayout.CreateUnionContours([
            new RectangleF(0, 0, 10, 10),
            new RectangleF(10, 10, 10, 10)
        ]);

        contours.Should().HaveCount(2);
        contours.Sum(GetArea).Should().BeApproximately(200, 0.001f);
    }

    [TestMethod]
    public void CreateRoundedContour_WhenRectangleIsLarge_UsesAdaptiveCircularCorners()
    {
        PointF[] contour = [
            new(0, 0),
            new(100, 0),
            new(100, 40),
            new(0, 40)
        ];

        IReadOnlyList<RecognizedTextRoundedCorner> corners =
            RecognizedTextLayout.CreateRoundedContour(contour);

        corners.Should().HaveCount(4);
        corners.Should().OnlyContain(corner => !corner.IsConcave);
        corners.Should().OnlyContain(corner => corner.SweepsClockwise);
        corners.Should().OnlyContain(corner => Math.Abs(corner.Radius - 8.8f) < 0.001f);
        corners[0].EntryPoint.Should().Be(new PointF(0, 8.8f));
        corners[0].ExitPoint.Should().Be(new PointF(8.8f, 0));
    }

    [TestMethod]
    public void CreateRoundedContour_WhenContourHasAnInset_RoundsOuterAndInnerCorners()
    {
        PointF[] contour = [
            new(0, 0),
            new(100, 0),
            new(100, 40),
            new(60, 40),
            new(60, 20),
            new(40, 20),
            new(40, 40),
            new(0, 40)
        ];

        IReadOnlyList<RecognizedTextRoundedCorner> corners =
            RecognizedTextLayout.CreateRoundedContour(contour);

        corners.Should().HaveCount(contour.Length);
        corners.Count(corner => corner.IsConcave).Should().Be(2);
        corners.Where(corner => corner.IsConcave)
            .Should().OnlyContain(corner => !corner.SweepsClockwise);
        corners.Should().OnlyContain(corner => corner.Radius > 0);

        for (int index = 0; index < corners.Count; index++)
        {
            float incomingLength = GetDistance(
                contour[(index + contour.Length - 1) % contour.Length],
                contour[index]);
            float outgoingLength = GetDistance(
                contour[index],
                contour[(index + 1) % contour.Length]);
            float maximumTrim = Math.Min(incomingLength, outgoingLength) / 2;

            GetDistance(corners[index].EntryPoint, corners[index].CornerPoint)
                .Should().BeLessThanOrEqualTo(maximumTrim + 0.001f);
            GetDistance(corners[index].CornerPoint, corners[index].ExitPoint)
                .Should().BeLessThanOrEqualTo(maximumTrim + 0.001f);
        }
    }

    [TestMethod]
    public void CreateRoundedContour_WhenEdgesAreTiny_DoesNotLetAdjacentCornersOverlap()
    {
        PointF[] contour = [
            new(0, 0),
            new(3, 0),
            new(3, 3),
            new(0, 3)
        ];

        IReadOnlyList<RecognizedTextRoundedCorner> corners =
            RecognizedTextLayout.CreateRoundedContour(contour);

        corners.Should().OnlyContain(corner => Math.Abs(corner.Radius - 1.5f) < 0.001f);
        for (int index = 0; index < corners.Count; index++)
        {
            RecognizedTextRoundedCorner current = corners[index];
            RecognizedTextRoundedCorner next = corners[(index + 1) % corners.Count];
            GetDistance(current.ExitPoint, next.EntryPoint).Should().BeApproximately(0, 0.001f);
        }
    }

    private static float GetArea(IReadOnlyList<PointF> contour)
    {
        float area = 0;
        for (int index = 0; index < contour.Count; index++)
        {
            PointF current = contour[index];
            PointF next = contour[(index + 1) % contour.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return Math.Abs(area) / 2;
    }

    private static RectangleF GetBounds(IReadOnlyList<PointF> contour)
    {
        float left = contour.Min(point => point.X);
        float top = contour.Min(point => point.Y);
        float right = contour.Max(point => point.X);
        float bottom = contour.Max(point => point.Y);
        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    private static float GetDistance(PointF first, PointF second)
    {
        float deltaX = second.X - first.X;
        float deltaY = second.Y - first.Y;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private static RecognizedTextRegion Word(
        string text,
        float x,
        float y,
        float width,
        float height,
        int lineIndex,
        int wordIndex)
    {
        return new RecognizedTextRegion(
            text,
            new RectangleF(x, y, width, height),
            lineIndex,
            wordIndex);
    }
}
