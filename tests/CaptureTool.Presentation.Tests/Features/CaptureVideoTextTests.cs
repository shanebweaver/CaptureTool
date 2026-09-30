using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Features.CaptureDetails;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class CaptureVideoTextTests
{
    [TestMethod]
    public void MomentsGroupTextAndEveryQrOccurrenceInTimestampOrder()
    {
        var vm = Create();
        vm.Replace([Passage("later", "Later words", 3), Passage("first", "First words", 1),
            Passage("paragraph", "Another paragraph", 1),
            new("qr", CaptureTextSource.QrCode, "QR", "https://example.com", [new("one", TimeSpan.FromSeconds(1), null), new("three", TimeSpan.FromSeconds(3), null)])]);
        CollectionAssert.AreEqual(new[] { 1d, 3d }, vm.Visible.Select(frame => frame.Time!.Value.TotalSeconds).ToArray());
        Assert.HasCount(3, vm.Visible[0].Passages);
        Assert.HasCount(2, vm.Visible[1].Passages);
        vm.SetNavigationContext(new(true, true));
        vm.Open(vm.Visible[1]);
        Assert.IsTrue(vm.HasSelectedFrame);
        Assert.IsTrue(vm.Content.SelectedPassage!.CanNavigate);
        Assert.IsTrue(vm.Content.Visible.All(passage => passage.SelectedLocation!.Time == TimeSpan.FromSeconds(3)));
        Assert.IsTrue(vm.Content.Visible.All(passage => !passage.IsTimestampVisible));
        Assert.AreEqual("Later words" + Environment.NewLine + "https://example.com", vm.Content.CopyVisibleScope());
    }

    [TestMethod]
    public void GlobalTextSearchFindsMomentsAndLocalSearchDoesNotChangeIt()
    {
        var vm = Create();
        vm.Replace([Passage("a", "Invoice header", 1), Passage("b", "Total 125", 1), Passage("c", "Invoice footer", 2), Passage("d", "Other", 3)]);
        vm.Query = "invoice";
        Assert.HasCount(2, vm.Visible);
        vm.Open(vm.Visible[0]);
        Assert.AreEqual("invoice", vm.Content.Query);
        Assert.AreEqual("Invoice header", vm.Content.CopyVisibleScope());
        vm.Content.Query = "total";
        Assert.AreEqual("Total 125", vm.Content.CopyVisibleScope());
        vm.BackCommand.Execute(null);
        Assert.IsFalse(vm.HasSelectedFrame);
        Assert.AreEqual("invoice", vm.Query);
        Assert.HasCount(2, vm.Visible);
        vm.Open(vm.Visible[1]);
        Assert.AreEqual("Invoice footer", vm.Content.CopyVisibleScope());
    }

    [TestMethod]
    public void TimestampSearchOpensAllItsWordsAndUnknownTimesCannotSeek()
    {
        var vm = Create();
        vm.Replace([Passage("a", "First", 1.25), Passage("b", "Second", 1.5),
            new("unknown", CaptureTextSource.ImageText, "Text", "Unknown", [new("Unknown", null, new(.1, .1, .2, .2))])]);
        Assert.AreEqual("0:01.250", vm.Visible[0].Label);
        Assert.AreEqual("0:01.500", vm.Visible[1].Label);
        Assert.IsNull(vm.Visible[2].Time);
        vm.SetNavigationContext(new(true, true));
        vm.Query = "0:01.500";
        vm.Open(vm.Visible.Single());
        Assert.AreEqual(string.Empty, vm.Content.Query);
        Assert.AreEqual("Second", vm.Content.CopyVisibleScope());
        vm.BackCommand.Execute(null);
        vm.Query = "Unknown";
        vm.Open(vm.Visible.Single());
        Assert.IsFalse(vm.Content.SelectedPassage!.CanNavigate);
    }

    [TestMethod]
    public void RefreshPreservesTheOpenMomentAndLocalQueryThenReturnsWhenItDisappears()
    {
        var vm = Create();
        var first = Passage("a", "First", 1);
        vm.Replace([first]);
        vm.Open(vm.Visible.Single());
        vm.Content.Query = "first";
        var selection = vm.Content.SelectedPassage;
        vm.Replace([Passage("a", "First", 1), Passage("b", "Later", 2)]);
        Assert.AreEqual(TimeSpan.FromSeconds(1), vm.SelectedFrame!.Time);
        Assert.AreEqual("first", vm.Content.Query);
        Assert.AreSame(selection, vm.Content.SelectedPassage);
        vm.Replace([Passage("b", "Later", 2)]);
        Assert.IsFalse(vm.HasSelectedFrame);
        Assert.IsFalse(vm.Content.HasText);
        vm.Replace([]);
        Assert.IsTrue(vm.HasNoMatches);
    }

    [TestMethod]
    public void SearchCoversUnloadedMomentsAndNavigationRespectsTrimAndSourceChanges()
    {
        var vm = Create();
        vm.Replace(Enumerable.Range(0, 250).Select(index => Passage(index.ToString(), "Frame " + index, index)).ToArray());
        Assert.HasCount(100, vm.Visible);
        Assert.IsTrue(vm.HasMore);
        vm.LoadMoreCommand.Execute(null);
        Assert.HasCount(200, vm.Visible);
        vm.Query = "Frame 249";
        Assert.HasCount(1, vm.Visible);
        Assert.IsFalse(vm.HasMore);
        vm.SetNavigationContext(new(true, true, StartSeconds: 240, EndSeconds: 250));
        vm.Open(vm.Visible.Single());
        Assert.IsTrue(vm.Content.SelectedPassage!.CanNavigate);
        vm.SetNavigationContext(new(true, true, StartSeconds: 0, EndSeconds: 100));
        Assert.IsFalse(vm.Content.SelectedPassage.CanNavigate);
        vm.SetNavigationContext(new(true, false));
        Assert.IsFalse(vm.Content.SelectedPassage.CanNavigate);
        vm.BackCommand.Execute(null);
        vm.Query = "no matches";
        Assert.IsTrue(vm.HasNoMatches);
    }

    private static CaptureTextPassage Passage(string id, string text, double seconds) =>
        new(id, CaptureTextSource.ImageText, "Text", text, [new("Time", TimeSpan.FromSeconds(seconds), null)]);
    private static CaptureVideoTextViewModel Create()
    {
        var text = new Mock<ILocalizationService>();
        text.Setup(x => x.GetString(It.IsAny<string>())).Returns((string key) => key);
        return new(text.Object);
    }
}
