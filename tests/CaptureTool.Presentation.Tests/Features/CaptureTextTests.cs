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
    public void RepeatedVideoFramesAreGroupedWithoutLosingOccurrences()
    {
        var record = Record(AnalysisMediaKind.Video, new TextRecognitionMetadata([
            new("First", timestamp: TimeSpan.FromSeconds(1)), new("Second", timestamp: TimeSpan.FromSeconds(1)),
            new("First", timestamp: TimeSpan.FromSeconds(2)), new("Second", timestamp: TimeSpan.FromSeconds(2)),
            new("Changed", timestamp: TimeSpan.FromSeconds(3)), new("First", timestamp: TimeSpan.FromSeconds(4))]));
        var passages = CaptureTextPassage.From(record, Text());
        Assert.HasCount(3, passages);
        Assert.AreEqual("First" + Environment.NewLine + "Second", passages[0].Text);
        CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, passages[0].Locations.Select(location => location.Time!.Value).ToArray());
        Assert.HasCount(1, passages[2].Locations);
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
    private static CaptureAnalysisRecord Record(AnalysisMediaKind kind, AnalysisPayload payload) =>
        new(CaptureId.New(), kind, new(new string('a', 64)), "test", Guid.NewGuid(),
            [new(payload, new("test", "test", "test", "1"), DateTimeOffset.UtcNow, "test")]);
    private static ILocalizationService Text()
    {
        var text = new Mock<ILocalizationService>();
        text.Setup(service => service.GetString(It.IsAny<string>())).Returns((string key) => key);
        return text.Object;
    }
}
