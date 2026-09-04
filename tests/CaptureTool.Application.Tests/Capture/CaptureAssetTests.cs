using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CaptureAssetTests
{
    private static readonly DateTimeOffset CapturedAtUtc = new(2026, 8, 6, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public void CaptureId_New_ShouldCreateUniqueNonEmptyIds()
    {
        CaptureId first = CaptureId.New();
        CaptureId second = CaptureId.New();

        Assert.IsFalse(first.IsEmpty);
        Assert.IsFalse(second.IsEmpty);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void CaptureId_ShouldRoundTripCanonicalText()
    {
        var value = new Guid("f92c24e0-efb6-41b2-a5e1-15752ca1c3b1");
        var id = new CaptureId(value);

        CaptureId parsed = CaptureId.Parse(id.ToString());

        Assert.AreEqual(id, parsed);
        Assert.AreEqual("f92c24e0-efb6-41b2-a5e1-15752ca1c3b1", id.ToString());
    }

    [TestMethod]
    public void CaptureId_ShouldRejectEmptyValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureId(Guid.Empty));
        Assert.IsFalse(CaptureId.TryParse(Guid.Empty.ToString(), out CaptureId parsed));
        Assert.IsTrue(parsed.IsEmpty);
    }

    [TestMethod]
    public void Create_ShouldInitializeActiveAssetAtFirstRevision()
    {
        CaptureAsset asset = CaptureAsset.Create(
            CaptureFileType.Image,
            @"C:\Captures\retained.png",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);

        Assert.IsFalse(asset.Id.IsEmpty);
        Assert.AreEqual(CaptureFileType.Image, asset.MediaType);
        Assert.AreEqual(@"C:\Captures\retained.png", asset.RetainedSourcePath);
        Assert.AreEqual(CaptureSourceOwnership.AppOwned, asset.SourceOwnership);
        Assert.IsNull(asset.PreferredOpenPath);
        Assert.AreEqual(CapturedAtUtc, asset.CapturedAtUtc);
        Assert.AreEqual(CaptureAssetLifecycleState.Active, asset.LifecycleState);
        Assert.AreEqual(1L, asset.LifecycleRevision);
    }

    [TestMethod]
    public void Constructor_ShouldRejectRelativePathsAndNonUtcCaptureTimes()
    {
        CaptureId id = CaptureId.New();

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAsset(
            id,
            CaptureFileType.Image,
            "retained.png",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc));

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAsset(
            id,
            CaptureFileType.Image,
            @"C:\Captures\retained.png",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc.ToOffset(TimeSpan.FromHours(-7))));

        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAsset(
            id,
            CaptureFileType.Image,
            @"C:\Captures\retained.png",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc,
            "export.png"));
    }

    [TestMethod]
    public void Constructor_ShouldRejectUnknownMediaType()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Unknown,
            @"C:\Captures\retained.data",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc));
    }

    [TestMethod]
    public void Constructor_ShouldRejectUnknownSourceOwnership()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            @"C:\Captures\retained.png",
            CaptureSourceOwnership.Unknown,
            CapturedAtUtc));
    }

    [TestMethod]
    public void ChangePreferredOpenPath_ShouldPreserveIdentityAndRetainedSource()
    {
        CaptureAsset asset = CreateAsset();
        CaptureId originalId = asset.Id;
        string originalSource = asset.RetainedSourcePath;

        CaptureAsset changed = asset.ChangePreferredOpenPath(@"D:\Exports\capture.png");

        Assert.AreEqual(originalId, changed.Id);
        Assert.AreEqual(originalSource, changed.RetainedSourcePath);
        Assert.AreEqual(@"D:\Exports\capture.png", changed.PreferredOpenPath);
        Assert.AreEqual(2L, changed.LifecycleRevision);
        Assert.IsNull(asset.PreferredOpenPath);
        Assert.AreEqual(1L, asset.LifecycleRevision);

        CaptureAsset unchanged = changed.ChangePreferredOpenPath(@"d:\exports\CAPTURE.png");
        Assert.AreSame(changed, unchanged);
    }

    [TestMethod]
    public void ChangeSource_ShouldUpdatePathAndOwnershipAtNextRevision()
    {
        CaptureAsset asset = CreateAsset();

        CaptureAsset changed = asset.ChangeSource(
            @"D:\Legacy\capture.png",
            CaptureSourceOwnership.LegacyExternal);

        Assert.AreEqual(@"D:\Legacy\capture.png", changed.RetainedSourcePath);
        Assert.AreEqual(CaptureSourceOwnership.LegacyExternal, changed.SourceOwnership);
        Assert.AreEqual(2L, changed.LifecycleRevision);
        Assert.AreEqual(@"C:\Captures\retained.png", asset.RetainedSourcePath);
        Assert.AreEqual(1L, asset.LifecycleRevision);
    }

    [TestMethod]
    public void MarkDeleted_ShouldBeIdempotentAndPreventFurtherChanges()
    {
        CaptureAsset asset = CreateAsset();

        CaptureAsset deleted = asset.MarkDeleted();
        Assert.AreEqual(CaptureAssetLifecycleState.Deleted, deleted.LifecycleState);
        Assert.AreEqual(2L, deleted.LifecycleRevision);
        Assert.AreEqual(CaptureAssetLifecycleState.Active, asset.LifecycleState);
        Assert.AreEqual(1L, asset.LifecycleRevision);
        Assert.AreSame(deleted, deleted.MarkDeleted());

        Assert.ThrowsExactly<InvalidOperationException>(() => deleted.ChangeSource(
            @"D:\Captures\replacement.png",
            CaptureSourceOwnership.AppOwned));
        Assert.ThrowsExactly<InvalidOperationException>(() => deleted.ChangePreferredOpenPath(
            @"D:\Exports\capture.png"));
    }

    [TestMethod]
    public void CaptureAssetChange_ShouldContainOnlyOrderedLifecycleFacts()
    {
        CaptureId id = CaptureId.New();
        var change = new CaptureAssetChange(
            12,
            id,
            3,
            CaptureAssetChangeType.SourceChanged,
            CapturedAtUtc);

        Assert.AreEqual(12L, change.Sequence);
        Assert.AreEqual(id, change.CaptureId);
        Assert.AreEqual(3L, change.LifecycleRevision);
        Assert.AreEqual(CaptureAssetChangeType.SourceChanged, change.ChangeType);
        Assert.AreEqual(CapturedAtUtc, change.ChangedAtUtc);
        Assert.IsFalse(typeof(CaptureAssetChange).GetProperties().Any(property => property.PropertyType == typeof(string)));
    }

    private static CaptureAsset CreateAsset()
    {
        return new(
            CaptureId.New(),
            CaptureFileType.Image,
            @"C:\Captures\retained.png",
            CaptureSourceOwnership.AppOwned,
            CapturedAtUtc);
    }
}
