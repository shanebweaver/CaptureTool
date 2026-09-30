using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Presentation.Features.CaptureDetails;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureTextTests
{
    [TestMethod]
    public void SavedAndEditorTextShareParagraphsCopySpacingAndExactOverlayLineMembership()
    {
        var source = new (string Text, int X, int Y)[] { ("Left first", 100, 100), ("Right first", 600, 100),
            ("Left second", 100, 128), ("Right second", 600, 128) };
        var words = source.Select((item, index) => new RecognizedText(item.Text,
            new(item.X / 1000d, item.Y / 1000d, .3, .02), lineIndex: index, wordIndex: 0)).ToArray();
        var saved = ImageRows(words);
        var document = new CaptureTool.Application.Abstractions.Edit.Image.TextExtraction.RecognizedTextDocument("unused", new(1000, 1000),
            source.Select((item, index) => new CaptureTool.Application.Abstractions.Edit.Image.TextExtraction.RecognizedTextRegion(
                item.Text, new(item.X, item.Y, 300, 20), index, 0)).ToArray());
        var editor = CaptureTextPassage.From(document, Text());
        CollectionAssert.AreEqual(saved.Select(passage => passage.Text).ToArray(), editor.Select(passage => passage.Text).ToArray());
        Assert.HasCount(2, saved);
        CollectionAssert.AreEqual(new int?[] { 0, 2 }, saved[0].TextRegions.Select(word => word.LineIndex).ToArray());
        foreach (var passages in new[] { saved, editor })
        {
            var overlay = CaptureImageTextOverlay.Create(passages, new(1000, 1000));
            CollectionAssert.AreEqual(new[] { "Left first", "Left second", "Right first", "Right second" }, overlay.Text.Select(word => word.Text).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, overlay.Text.Select(word => word.ParagraphIndex).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, overlay.Text.Select(word => word.LineIndex).ToArray());
            Assert.AreEqual(new System.Drawing.RectangleF(100, 128, 300, 20), overlay.Text[1].Bounds);
            var viewModel = new CaptureTextViewModel(Text());
            viewModel.Replace(passages);
            Assert.AreEqual(string.Join(Environment.NewLine + Environment.NewLine, saved.Select(passage => passage.Text)), viewModel.CopyVisibleScope());
        }
    }

    [TestMethod]
    public void PassageSelectionUsesOnlyItsOriginalWords()
    {
        var rows = ImageRows([Word("Invoice", .1, .1, .07), Word("total", .18, .102, .05),
            Word("Pay", .1, .128, .03), Word("today.", .14, .128, .06), Word("Other column", .6, .1, .2)]);
        var passage = rows.First();
        var regions = CaptureImageTextOverlay.CreateSelectionRegions(passage, new(1000, 500));

        CollectionAssert.AreEqual(new[] { "Invoice", "total", "Pay", "today." }, regions.Select(region => region.Text).ToArray());
        CollectionAssert.AreEqual(new[] { new System.Drawing.RectangleF(100, 50, 70, 10), new System.Drawing.RectangleF(180, 51, 50, 10),
            new System.Drawing.RectangleF(100, 64, 30, 10), new System.Drawing.RectangleF(140, 64, 60, 10) }, regions.Select(region => region.Bounds).ToArray());
    }

    [TestMethod]
    public void QrSelectionHighlightsOnlyTheChosenOccurrence()
    {
        var passage = new CaptureTextPassage("qr", CaptureTextSource.QrCode, "QR", "https://example.com",
            [new("First", null, new(.1, .1, .2, .2)), new("Second", null, new(.6, .5, .2, .2))]);
        passage.SelectedLocation = passage.Locations[1];
        var regions = CaptureImageTextOverlay.CreateSelectionRegions(passage, new(1000, 500));

        Assert.HasCount(1, regions);
        Assert.AreEqual(passage.Text, regions[0].Text);
        Assert.AreEqual(new System.Drawing.RectangleF(600, 250, 200, 100), regions[0].Bounds);
    }

    [TestMethod]
    public void ImageSelectionDoesNotInventBoundsForTimedOrUnlocatedText()
    {
        var image = ImageRows([new("No bounds")]).Single();
        Assert.IsEmpty(CaptureImageTextOverlay.CreateSelectionRegions(image, new(1000, 500)));
        Assert.IsEmpty(CaptureImageTextOverlay.CreateSelectionRegions(Passage("speech", "Transcript"), new(1000, 500)));
        var located = ImageRows([Word("Word", .1, .1, .1)]).Single();
        Assert.IsEmpty(CaptureImageTextOverlay.CreateSelectionRegions(located, System.Drawing.Size.Empty));
    }

    [TestMethod]
    public void ImageWordsBecomeSearchableParagraphsWithOriginalOverlayBounds()
    {
        RecognizedText[] words = [Word("Invoice", .1, .1, .07), Word("total", .18, .102, .05),
            Word("Pay", .1, .128, .03), Word("today.", .14, .128, .06)];
        var record = Record(AnalysisMediaKind.Image, new TextRecognitionMetadata(words));
        var rows = CaptureTextPassage.From(record, Text());
        Assert.HasCount(1, rows);
        Assert.AreEqual("Invoice total" + Environment.NewLine + "Pay today.", rows[0].Text);
        Assert.HasCount(1, rows[0].Locations);
        Assert.IsFalse(rows[0].HasOccurrences);
        var bounds = rows[0].Locations[0].Bounds!;
        Assert.AreEqual(.1, bounds.X, .000001);
        Assert.AreEqual(.13, bounds.Width, .000001);
        Assert.AreEqual(.048, bounds.Height, .000001);
        CollectionAssert.AreEqual(words, rows[0].TextRegions.ToArray());
        CollectionAssert.AreEqual(words, ((TextRecognitionMetadata)record.Results[0].Payload).Regions.ToArray());

        var overlay = CaptureImageTextOverlay.Create(rows, new(1000, 500));
        Assert.HasCount(4, overlay.Text);
        Assert.AreEqual("total", overlay.Text[1].Text);
        Assert.AreEqual(new System.Drawing.RectangleF(180, 51, 50, 10), overlay.Text[1].Bounds);
        var vm = new CaptureTextViewModel(Text());
        vm.Replace(rows);
        vm.Query = "Invoice total";
        Assert.HasCount(1, vm.Visible);
        Assert.AreEqual(rows[0].Text, vm.CopyVisibleScope());
    }

    [TestMethod]
    public void ColumnsHeadingsParagraphBreaksAndListItemsRemainSeparate()
    {
        var rows = ImageRows([
            Word("Heading", .1, .02, .21, .04),
            Word("Left", .1, .08, .04), Word("column", .15, .08, .06),
            Word("Right", .6, .08, .05), Word("column", .66, .08, .06),
            Word("New paragraph", .1, .18, .13),
            Word("• First item", .1, .208, .12),
            Word("• Second item", .1, .236, .13),
            Word("1. Numbered item", .1, .264, .16)]);
        CollectionAssert.AreEqual(new[] { "Heading", "Left column", "Right column", "New paragraph",
            "• First item", "• Second item", "1. Numbered item" }, rows.Select(row => row.Text).ToArray());
    }

    [TestMethod]
    public void ColumnMajorProviderOrderAndRightToLeftWordOrderArePreserved()
    {
        var columns = ImageRows([Word("Left first", .1, .1, .1), Word("Left second", .1, .128, .11),
            Word("Right first", .6, .1, .11), Word("Right second", .6, .128, .12)]);
        CollectionAssert.AreEqual(new[] { "Left first" + Environment.NewLine + "Left second",
            "Right first" + Environment.NewLine + "Right second" }, columns.Select(row => row.Text).ToArray());
        var rtl = ImageRows([Word("שלום", .6, .1, .04), Word("עולם", .55, .1, .04)]);
        Assert.AreEqual("שלום עולם", rtl.Single().Text);
    }

    [TestMethod]
    public void UnknownBoundsAndAlreadyMultilineTextRemainCopyableWithoutInventedLocations()
    {
        const string multiline = "Already grouped\nwith line breaks";
        var rows = ImageRows([Word("Before", .1, .1, .06), new("Unknown"),
            new("Empty bounds", new(.1, .128, 0, .02)), Word(multiline, .1, .156, .2), Word("After", .1, .184, .05)]);
        CollectionAssert.AreEqual(new[] { "Before", "Unknown", "Empty bounds", multiline, "After" }, rows.Select(row => row.Text).ToArray());
        Assert.IsNull(rows[1].Locations[0].Bounds);
        Assert.IsNull(rows[2].Locations[0].Bounds);
        Assert.HasCount(3, CaptureImageTextOverlay.Create(rows, new(1000, 500)).Text);
    }

    [TestMethod]
    public void GroupingUsesEachNormalizedAxisIndependently()
    {
        foreach (double height in new[] { .002, .2 })
        {
            var rows = ImageRows([Word("Wide", .1, .1, .04, height), Word("screen", .15, .1, .06, height),
                Word("Sidebar", .8, .1, .07, height)]);
            CollectionAssert.AreEqual(new[] { "Wide screen", "Sidebar" }, rows.Select(row => row.Text).ToArray());
        }
        var edge = ImageRows([Word("At", .9, .98, .02), Word("edge", .93, .98, .07)]).Single();
        Assert.AreEqual("At edge", edge.Text);
        Assert.AreEqual(1, edge.Locations[0].Bounds!.X + edge.Locations[0].Bounds!.Width, .000001);
    }

    [TestMethod]
    public void VideoWordsKeepTheirOwnFrameGroupingAndWordBounds()
    {
        var regions = new[] { 1, 2, 3 }.SelectMany(second => new[] {
            Word("Frame", .1, .1, .05), Word(second == 3 ? "changed" : "text", .16, .1, .07)
        }.Select(word => new RecognizedText(word.Text, word.Bounds, TimeSpan.FromSeconds(second))));
        var rows = CaptureTextPassage.From(Record(AnalysisMediaKind.Video, new TextRecognitionMetadata(regions)), Text());
        Assert.HasCount(3, rows);
        Assert.AreEqual("Frame text", rows[0].Text);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3) },
            rows.Select(row => row.SelectedLocation!.Time!.Value).ToArray());
        Assert.AreEqual("Frame changed", rows[2].Text);
        Assert.HasCount(2, rows[0].TextRegions);
        Assert.HasCount(1, rows[0].TextLines);
        Assert.IsTrue(rows[1].TextRegions.All(word => word.Timestamp == TimeSpan.FromSeconds(2)));
    }

    [TestMethod]
    public void WordBoundsChangesRefreshOverlayEvenWhenParagraphTextAndUnionAreUnchanged()
    {
        var vm = new CaptureTextViewModel(Text());
        var words = new[] { Word("First", .1, .1, .05), Word("second", .16, .1, .06) };
        var record = Record(AnalysisMediaKind.Image, new TextRecognitionMetadata(words));
        var row = CaptureTextPassage.From(record, Text()).Single();
        vm.Replace([row]);
        var changed = new CaptureTextPassage(row.Id, row.Source, row.Label, row.Text, row.Locations,
            [Word("First", .1, .1, .06), words[1]]);
        vm.Replace([changed]);
        Assert.AreSame(changed, vm.Visible.Single());
    }

    [TestMethod]
    public void RepeatedVideoFramesKeepSeparatePassagesAtEveryTimestamp()
    {
        var record = Record(AnalysisMediaKind.Video, new TextRecognitionMetadata([
            new("First", timestamp: TimeSpan.FromSeconds(1)), new("Second", timestamp: TimeSpan.FromSeconds(1)),
            new("First", timestamp: TimeSpan.FromSeconds(2)), new("Second", timestamp: TimeSpan.FromSeconds(2)),
            new("Changed", timestamp: TimeSpan.FromSeconds(3)), new("First", timestamp: TimeSpan.FromSeconds(4))]));
        var passages = CaptureTextPassage.From(record, Text());
        Assert.HasCount(6, passages);
        CollectionAssert.AreEqual(new[] { "First", "Second", "First", "Second", "Changed", "First" }, passages.Select(passage => passage.Text).ToArray());
        CollectionAssert.AreEqual(new[] { 1d, 1d, 2d, 2d, 3d, 4d }, passages.Select(passage => passage.SelectedLocation!.Time!.Value.TotalSeconds).ToArray());
    }

    [TestMethod]
    public void LargeTranscriptSearchAndCopyCoverRowsBeyondFirstPage()
    {
        var vm = new CaptureTextViewModel(Text());
        var passages = Enumerable.Range(0, 350).Select(index => Passage(index.ToString(), "Meeting résumé " + index)).ToArray();
        vm.Replace(passages);
        Assert.HasCount(100, vm.Visible);
        Assert.IsTrue(vm.HasMore);
        Assert.Contains("Meeting résumé 349", vm.CopyVisibleScope());
        vm.Query = "RÉSUMÉ 349";
        Assert.HasCount(1, vm.Visible);
        Assert.AreSame(passages[349], vm.Visible[0]);
        Assert.AreEqual("Meeting résumé 349", vm.CopyVisibleScope());
        vm.NextCommand.Execute(null);
        Assert.AreSame(passages[349], vm.SelectedPassage);
        vm.Query = "absent";
        Assert.IsEmpty(vm.Visible);
        Assert.IsFalse(vm.NextCommand.CanExecute(null));
        Assert.AreEqual(string.Empty, vm.CopyVisibleScope());
    }

    [TestMethod]
    public void NavigationStopsAtBoundariesAndLoadsOnlyTheNextPage()
    {
        var vm = new CaptureTextViewModel(Text());
        vm.Replace(Enumerable.Range(0, 350).Select(index => Passage(index.ToString(), "Text " + index)).ToArray());
        Assert.IsFalse(vm.PreviousCommand.CanExecute(null));
        vm.NextCommand.Execute(null);
        Assert.IsFalse(vm.PreviousCommand.CanExecute(null));
        Assert.HasCount(100, vm.Visible);
        vm.SelectedPassage = vm.Visible.Last();
        vm.NextCommand.Execute(null);
        Assert.HasCount(200, vm.Visible);
        Assert.AreEqual("100", vm.SelectedPassage!.Id);
        vm.Query = "Text 349";
        vm.NextCommand.Execute(null);
        Assert.IsFalse(vm.NextCommand.CanExecute(null));
        Assert.IsFalse(vm.PreviousCommand.CanExecute(null));
        Assert.HasCount(1, vm.Visible);
    }

    [TestMethod]
    public void UnchangedRefreshPreservesRowsSelectionAndFilterQuery()
    {
        var vm = new CaptureTextViewModel(Text());
        vm.Replace([Passage("a", "same text")]);
        vm.Query = "same";
        vm.NextCommand.Execute(null);
        var row = vm.Visible.Single();
        vm.Replace([Passage("a", "same text"), Passage("b", "other text")]);
        Assert.AreSame(row, vm.Visible.Single());
        Assert.AreSame(row, vm.SelectedPassage);
        Assert.AreEqual("same", vm.Query);
        vm.Replace([]);
        Assert.IsNull(vm.SelectedPassage);
        Assert.AreEqual(string.Empty, vm.CopyVisibleScope());
    }

    [TestMethod]
    public void FilteringChangesCopyScopeWithoutLosingTheQuery()
    {
        var vm = new CaptureTextViewModel(Text());
        vm.Replace([Passage("a", "capture speech"), new("b", CaptureTextSource.ImageText, "Text", "capture screen", [])]);
        vm.Query = "capture";
        vm.SelectedFilter = vm.Filters.Single(filter => filter.Source == CaptureTextSource.ImageText);
        Assert.AreEqual("capture screen", vm.CopyVisibleScope());
        Assert.AreEqual("capture", vm.Query);
    }

    [TestMethod]
    [DataRow(AnalysisMediaKind.Image)]
    [DataRow(AnalysisMediaKind.Audio)]
    [DataRow(AnalysisMediaKind.Video)]
    public void SourceChoicesReflectAvailableCaptureResults(AnalysisMediaKind kind)
    {
        var vm = new CaptureTextViewModel(Text());
        Assert.IsFalse(vm.HasSources);
        Assert.HasCount(1, vm.Filters);
        var text = new TextRecognitionMetadata([new("Screen text", new(.1, .1, .2, .02))]);
        var speech = new TranscriptMetadata("en", [new("Spoken text", TimeSpan.Zero, TimeSpan.FromSeconds(1))]);
        var qr = new QrCodeMetadata([new("https://example.com", new(.1, .1, .2, .2))]);
        AnalysisPayload[] payloads = kind switch
        {
            AnalysisMediaKind.Image => [text, qr],
            AnalysisMediaKind.Audio => [speech],
            _ => [text, speech, qr]
        };
        CaptureTextSource?[] expected = kind switch
        {
            AnalysisMediaKind.Image => [null, CaptureTextSource.ImageText, CaptureTextSource.QrCode],
            AnalysisMediaKind.Audio => [null, CaptureTextSource.Speech],
            _ => [null, CaptureTextSource.ImageText, CaptureTextSource.Speech, CaptureTextSource.QrCode]
        };
        vm.Replace(CaptureTextPassage.From(Record(kind, payloads), Text()));
        CollectionAssert.AreEqual(expected, vm.Filters.Select(filter => filter.Source).ToArray());
        Assert.AreEqual(kind != AnalysisMediaKind.Audio, vm.HasSources);
        vm.Query = "no matches";
        CollectionAssert.AreEqual(expected, vm.Filters.Select(filter => filter.Source).ToArray(),
            "Searching must not remove source choices or shift the selector.");
    }

    [TestMethod]
    public void SourceChangesResetHiddenFiltersAndPreserveSelectionsThatStillApply()
    {
        var vm = new CaptureTextViewModel(Text());
        var text = new CaptureTextPassage("text", CaptureTextSource.ImageText, "Text", "capture screen", []);
        var qr = new CaptureTextPassage("qr", CaptureTextSource.QrCode, "QR", "capture code", []);
        vm.Replace([text, qr]);
        vm.Query = "capture";
        var selected = vm.Filters.Single(filter => filter.Source == CaptureTextSource.QrCode);
        vm.SelectedFilter = selected;
        vm.Replace([text, Passage("speech", "capture speech"), qr]);
        Assert.AreSame(selected, vm.SelectedFilter);
        vm.SelectedFilter = null;
        Assert.AreSame(selected, vm.SelectedFilter, "Transient binding updates must preserve the selection.");
        Assert.AreEqual("capture code", vm.CopyVisibleScope());

        vm.Replace([text]);
        Assert.IsFalse(vm.HasSources);
        Assert.AreSame(vm.Filters[0], vm.SelectedFilter);
        Assert.AreEqual("capture", vm.Query);
        Assert.AreEqual("capture screen", vm.CopyVisibleScope());
        vm.Replace([text, qr]);
        Assert.IsTrue(vm.HasSources);
        Assert.HasCount(2, vm.Visible);
        vm.SelectedFilter = vm.Filters.Single(filter => filter.Source == CaptureTextSource.ImageText);
        vm.Replace([text]);
        Assert.AreSame(vm.Filters[0], vm.SelectedFilter, "A hidden selector should return to All sources.");
        vm.Replace([]);
        Assert.IsFalse(vm.HasSources);
        Assert.HasCount(1, vm.Filters);
    }

    [TestMethod]
    public void RecycledOccurrenceSelectionCannotNavigateToAnotherPassage()
    {
        var first = Passage("first", "First", TimeSpan.FromSeconds(1));
        var second = Passage("second", "Second", TimeSpan.FromSeconds(10));
        second.SelectedLocation = first.SelectedLocation;
        Assert.AreEqual(TimeSpan.FromSeconds(10), second.SelectedLocation!.Time);
        second.SelectedLocation = null;
        Assert.AreEqual(TimeSpan.FromSeconds(10), second.SelectedLocation!.Time);
    }

    [TestMethod]
    public void LocationsRequireMatchingReadySourceAndRespectImageEditsAndTrim()
    {
        var timed = Passage("a", "speech", TimeSpan.FromSeconds(10));
        timed.SetNavigationContext(new(true, true, StartSeconds: 5, EndSeconds: 15), "Unavailable");
        Assert.IsTrue(timed.CanNavigate);
        timed.SetNavigationContext(new(true, true, StartSeconds: 11, EndSeconds: 15), "Outside trim");
        Assert.IsFalse(timed.CanNavigate);
        Assert.AreEqual("Outside trim", timed.NavigationHint);
        timed.SetNavigationContext(new(true, false), "Changed source");
        Assert.IsFalse(timed.CanNavigate);
        var image = new CaptureTextPassage("image", CaptureTextSource.ImageText, "Text", "Word", [new("Text", null, new(.1, .1, .2, .2))]);
        image.SetNavigationContext(new(true, true, ImageEdited: true), "Edited image");
        Assert.IsFalse(image.CanNavigate);
        image.SetNavigationContext(new(true, true), "Unavailable");
        Assert.IsTrue(image.CanNavigate);
    }

    [TestMethod]
    public void QrValuesAreDeduplicatedAndOnlyWebTargetsCanOpen()
    {
        var bounds = new NormalizedBounds(.1, .1, .2, .2);
        var record = Record(AnalysisMediaKind.Video, new QrCodeMetadata([
            new("https://example.com/", bounds, TimeSpan.FromSeconds(1)), new("https://example.com/", bounds, TimeSpan.FromSeconds(2)),
            new("file:///C:/run.exe", bounds), new("javascript:alert(1)", bounds), new("arbitrary text", bounds)]));
        var rows = CaptureTextPassage.From(record, Text());
        Assert.HasCount(4, rows);
        var web = rows.Single(row => row.HasWebLink);
        Assert.HasCount(2, web.Locations);
        Assert.AreEqual("https://example.com/", web.WebUri!.AbsoluteUri);
    }

    private static CaptureTextPassage Passage(string id, string text, TimeSpan? time = null) =>
        new(id, CaptureTextSource.Speech, "Speech", text, [new("Position", time ?? TimeSpan.Zero, null)]);
    private static RecognizedText Word(string text, double x, double y, double width, double height = .02) =>
        new(text, new(x, y, width, height));
    private static IReadOnlyList<CaptureTextPassage> ImageRows(RecognizedText[] words) =>
        CaptureTextPassage.From(Record(AnalysisMediaKind.Image, new TextRecognitionMetadata(words)), Text());
    private static CaptureAnalysisRecord Record(AnalysisMediaKind kind, params AnalysisPayload[] payloads) =>
        new(CaptureId.New(), kind, new(new string('a', 64)), "test", Guid.NewGuid(),
            payloads.Select(payload => new AnalysisResult(payload, new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "test")));
    private static ILocalizationService Text()
    {
        var text = new Mock<ILocalizationService>();
        text.Setup(service => service.GetString(It.IsAny<string>())).Returns((string key) => key);
        return text.Object;
    }
}
