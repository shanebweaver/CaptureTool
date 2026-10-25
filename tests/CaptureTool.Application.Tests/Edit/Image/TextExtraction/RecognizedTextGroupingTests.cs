using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Analysis.TextLayout;
using System.Text.Json;

namespace CaptureTool.Application.Tests.Edit.Image.TextExtraction;

[TestClass]
public sealed class RecognizedTextGroupingTests
{
    [TestMethod]
    public void IllustratedFixtureMatchesItsParagraphsAndColumnReadingOrder()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text-layout.json")));
        double width = fixture.RootElement.GetProperty("Width").GetDouble(), height = fixture.RootElement.GetProperty("Height").GetDouble();
        var words = fixture.RootElement.GetProperty("Words").EnumerateArray().Select(word => new RecognizedText(
            word.GetProperty("Text").GetString()!, new(word.GetProperty("X").GetDouble() / width, word.GetProperty("Y").GetDouble() / height,
                word.GetProperty("Width").GetDouble() / width, word.GetProperty("Height").GetDouble() / height),
            lineIndex: word.GetProperty("LineIndex").GetInt32(), wordIndex: word.GetProperty("WordIndex").GetInt32())).ToArray();
        string[] expected = fixture.RootElement.GetProperty("Paragraphs").EnumerateArray()
            .Select(paragraph => paragraph.GetString()!.Replace("\n", Environment.NewLine)).ToArray();
        var actual = RecognizedTextGrouping.Create(words).Select(paragraph => paragraph.Text).ToArray();
        Assert.AreEqual(string.Join("\n---\n", expected), string.Join("\n---\n", actual),
            string.Join("\n\n", actual.Select((text, index) => $"{index}: {text}")));
    }

    [TestMethod]
    public void MultipleParagraphsReadDownEachColumnBeforeMovingToTheNext()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("Left first", .1, .1, .3, line: 0), Word("Right first", .6, .1, .3, line: 1),
            Word("Left continuation", .1, .128, .3, line: 2), Word("Right continuation", .6, .128, .3, line: 3),
            Word("Left next paragraph", .1, .19, .3, line: 4), Word("Right next paragraph", .6, .19, .3, line: 5)]);
        CollectionAssert.AreEqual(new[] { Lines("Left first", "Left continuation"), "Left next paragraph",
            Lines("Right first", "Right continuation"), "Right next paragraph" }, paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void IndentedFirstLineStartsANewParagraphAndItsUnindentedContinuationStaysWithIt()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("A preceding paragraph ends here.", .1, .1, .3, line: 0),
            Word("An indented paragraph starts.", .14, .128, .26, line: 1),
            Word("Its body continues on this line.", .1, .156, .3, line: 2),
            Word("And continues on another line.", .1, .184, .3, line: 3)]);
        CollectionAssert.AreEqual(new[] { "A preceding paragraph ends here.", Lines("An indented paragraph starts.",
            "Its body continues on this line.", "And continues on another line.") }, paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void DenseRowsDoNotLoseOrDuplicateWordsWhenNeighborSearchIsBounded()
    {
        var words = Enumerable.Range(0, 2000).Select(i => Word("label", i / 2000d, .1, .0001, line: i)).ToArray();
        var paragraphs = RecognizedTextGrouping.Create(words);
        Assert.HasCount(words.Length, paragraphs);
        CollectionAssert.AreEqual(Enumerable.Range(0, words.Length).ToArray(),
            paragraphs.SelectMany(paragraph => paragraph.Lines).SelectMany(line => line.WordIndices).Order().ToArray());
    }

    [TestMethod]
    public void AlternatingColumnRowsBecomeSeparateParagraphsWithoutChangingEvidence()
    {
        RecognizedText[] words = [Word("Left first", .1, .1, .3, line: 0), Word("Right first", .6, .1, .3, line: 1),
            Word("Left second", .1, .128, .3, line: 2), Word("Right second", .6, .128, .3, line: 3)];
        var paragraphs = RecognizedTextGrouping.Create(words);
        CollectionAssert.AreEqual(new[] { Lines("Left first", "Left second"), Lines("Right first", "Right second") },
            paragraphs.Select(paragraph => paragraph.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 2 }, paragraphs[0].Lines.SelectMany(line => line.WordIndices).ToArray());
        CollectionAssert.AreEqual(new[] { "Left first", "Right first", "Left second", "Right second" }, words.Select(word => word.Text).ToArray());
    }

    [TestMethod]
    public void LocalSpacingHandlesLooseLeadingAndSeparatesLargerParagraphGaps()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("Line one", .1, .1, .3, line: 0), Word("Line two", .1, .14, .3, line: 1),
            Word("Line three", .1, .18, .3, line: 2), Word("Next paragraph", .1, .24, .3, line: 3),
            Word("Its continuation", .1, .28, .3, line: 4)]);
        CollectionAssert.AreEqual(new[] { Lines("Line one", "Line two", "Line three"), Lines("Next paragraph", "Its continuation") },
            paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void WrappedBulletAndNumberedItemsStayTogetherAndSeparateFromEachOther()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("• First list entry", .1, .1, .18, line: 0), Word("wrapped entry text", .12, .128, .18, line: 1),
            Word("• Second item", .1, .156, .13, line: 2), Word("1. Numbered entry", .1, .184, .17, line: 3),
            Word("wrapped numbered text", .13, .212, .21, line: 4)]);
        CollectionAssert.AreEqual(new[] { Lines("• First list entry", "wrapped entry text"), "• Second item",
            Lines("1. Numbered entry", "wrapped numbered text") }, paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void ProviderLineHintsKeepMixedSizesTogetherButNeverBridgeColumns()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("Hello", .1, .1, .05, .03, 0), Word("world", .16, .11, .05, .02, 0),
            Word("Sidebar", .7, .1, .07, .02, 0)]);
        CollectionAssert.AreEqual(new[] { "Hello world", "Sidebar" }, paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void DistinctProviderLinesAreNotMergedIntoOneVisualLine()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("Top", .1, .1, .1, .025, 0), Word("Bottom", .1, .12, .1, .025, 1)]);
        Assert.HasCount(1, paragraphs);
        Assert.HasCount(2, paragraphs[0].Lines);
        Assert.AreEqual(Lines("Top", "Bottom"), paragraphs[0].Text);
    }

    [TestMethod]
    public void SpanningHeadingStopsBothColumnsFromJoiningAcrossIt()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("Left above", .1, .1, .3, line: 0), Word("Right above", .6, .1, .3, line: 1),
            Word("A heading spanning both columns", .1, .13, .8, .03, 2),
            Word("Left below", .1, .17, .3, line: 3), Word("Right below", .6, .17, .3, line: 4)]);
        Assert.HasCount(5, paragraphs);
    }

    [TestMethod]
    public void RightToLeftWordsAndColumnsKeepProviderOrder()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            Word("שלום", .8, .1, .04, line: 0), Word("עולם", .75, .1, .04, line: 0),
            Word("ימין", .78, .128, .06, line: 1), Word("שמאל", .1, .1, .08, line: 2)]);
        CollectionAssert.AreEqual(new[] { Lines("שלום עולם", "ימין"), "שמאל" }, paragraphs.Select(paragraph => paragraph.Text).ToArray());
    }

    [TestMethod]
    public void LayoutIsStableAcrossIndependentAxisScalingAndKeepsEveryWordExactlyOnce()
    {
        RecognizedText[] words = [Word("First column", .1, .1, .2, line: 0), Word("Other column", .6, .1, .2, line: 1),
            Word("Second line", .1, .128, .2, line: 2), Word("Other line", .6, .128, .2, line: 3),
            new("No bounds"), Word("• List entry", .1, .2, .12, line: 4), Word("列表文本", .1, .3, .12, line: 5)];
        var expected = RecognizedTextGrouping.Create(words).Select(paragraph => paragraph.Text).ToArray();
        foreach (var (sx, sy) in new[] { (.1, 1d), (1d, .1), (.4, .7) })
        {
            var scaled = words.Select(word => new RecognizedText(word.Text, word.Bounds is { } b
                ? new(b.X * sx, b.Y * sy, b.Width * sx, b.Height * sy) : null, word.Timestamp, word.LineIndex, word.WordIndex)).ToArray();
            var actual = RecognizedTextGrouping.Create(scaled);
            CollectionAssert.AreEqual(expected, actual.Select(paragraph => paragraph.Text).ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(0, words.Length).ToArray(), actual.SelectMany(paragraph => paragraph.Lines)
                .SelectMany(line => line.WordIndices).Order().ToArray());
        }
    }

    [TestMethod]
    public void DifferentVideoFramesAndUnlocatedTextAreGroupingBoundaries()
    {
        var paragraphs = RecognizedTextGrouping.Create([
            new("Frame one", new(.1, .1, .2, .02), TimeSpan.FromSeconds(1), 0, 0),
            new("Frame two", new(.1, .128, .2, .02), TimeSpan.FromSeconds(2), 0, 0),
            new("Unknown"), new("Already\nmultiline", new(.1, .156, .2, .04))]);
        Assert.HasCount(4, paragraphs);
        Assert.IsNull(paragraphs[2].Bounds);
        Assert.AreEqual("Already\nmultiline", paragraphs[3].Text);
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
    private static RecognizedText Word(string text, double x, double y, double width, double height = .02, int? line = null) =>
        new(text, new(x, y, width, height), lineIndex: line, wordIndex: line != null ? 0 : null);
}
