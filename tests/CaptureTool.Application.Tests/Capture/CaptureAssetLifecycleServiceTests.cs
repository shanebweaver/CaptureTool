using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Time;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Capture;

[TestClass]
public sealed class CaptureAssetLifecycleServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    [TestMethod]
    public void TryFinalize_ShouldCommitIdentityProjectRecentHistoryAndSignal()
    {
        LifecycleFixture fixture = new();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Returns<CaptureAsset>(asset => CaptureAssetCatalogWriteResult.Committed(asset, 12));
        fixture.RecentCatalog
            .Setup(catalog => catalog.TryProjectCaptured(
                It.IsAny<string>(),
                It.IsAny<CaptureFileType>(),
                It.IsAny<CaptureId>(),
                It.IsAny<long>(),
                It.IsAny<DateTime>()))
            .Returns(true);

        CaptureId? captureId = fixture.Service.TryFinalize(retainedPath, CaptureFileType.Image);

        Assert.IsNotNull(captureId);
        fixture.AssetCatalog.Verify(catalog => catalog.TryAdd(It.Is<CaptureAsset>(asset =>
            asset.Id == captureId &&
            asset.RetainedSourcePath == retainedPath &&
            asset.PreferredOpenPath == null &&
            asset.SourceOwnership == CaptureSourceOwnership.AppOwned &&
            asset.CapturedAtUtc == new DateTimeOffset(UtcNow))), Times.Once);
        fixture.RecentCatalog.Verify(catalog => catalog.TryProjectCaptured(
            retainedPath,
            CaptureFileType.Image,
            captureId.Value,
            12,
            UtcNow), Times.Once);
        fixture.ChangeSignal.Verify(signal => signal.TrySignal(), Times.Once);
    }

    [TestMethod]
    public void TryFinalize_WhenAssetStoreFails_ShouldKeepCaptureReachableWithoutIdentity()
    {
        LifecycleFixture fixture = new();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Returns(CaptureAssetCatalogWriteResult.Failed);

        CaptureId? captureId = fixture.Service.TryFinalize(retainedPath, CaptureFileType.Image);

        Assert.IsNull(captureId);
        fixture.RecentCatalog.Verify(
            catalog => catalog.RecordCaptured(retainedPath, CaptureFileType.Image),
            Times.Once);
        fixture.ChangeSignal.Verify(signal => signal.TrySignal(), Times.Never);
    }

    [TestMethod]
    public void TryFinalize_WhenAssetStoreThrows_ShouldKeepCaptureReachableWithoutIdentity()
    {
        LifecycleFixture fixture = new();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Throws(new IOException("asset store unavailable"));

        CaptureId? captureId = fixture.Service.TryFinalize(retainedPath, CaptureFileType.Image);

        Assert.IsNull(captureId);
        fixture.RecentCatalog.Verify(
            catalog => catalog.RecordCaptured(retainedPath, CaptureFileType.Image),
            Times.Once);
        fixture.ChangeSignal.Verify(signal => signal.TrySignal(), Times.Never);
    }

    [TestMethod]
    public void TryFinalize_WhenRecentStoreAndWakeThrow_ShouldNotFailSuccessfulCapture()
    {
        LifecycleFixture fixture = new();
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Returns<CaptureAsset>(asset => CaptureAssetCatalogWriteResult.Committed(asset, 1));
        fixture.RecentCatalog
            .Setup(catalog => catalog.TryProjectCaptured(
                It.IsAny<string>(),
                It.IsAny<CaptureFileType>(),
                It.IsAny<CaptureId>(),
                It.IsAny<long>(),
                It.IsAny<DateTime>()))
            .Throws(new IOException("recent store unavailable"));
        fixture.ChangeSignal
            .Setup(signal => signal.TrySignal())
            .Throws(new InvalidOperationException("wake unavailable"));

        CaptureId? captureId = fixture.Service.TryFinalize(
            @"C:\CaptureTool\Captures\capture.png",
            CaptureFileType.Image);

        Assert.IsNotNull(captureId);
    }

    [TestMethod]
    public void TryFinalize_WhenWakeChannelIsFull_ShouldNotDelayOrFailSuccessfulCapture()
    {
        LifecycleFixture fixture = new();
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Returns<CaptureAsset>(asset => CaptureAssetCatalogWriteResult.Committed(asset, 1));
        fixture.RecentCatalog
            .Setup(catalog => catalog.TryProjectCaptured(
                It.IsAny<string>(),
                It.IsAny<CaptureFileType>(),
                It.IsAny<CaptureId>(),
                It.IsAny<long>(),
                It.IsAny<DateTime>()))
            .Returns(true);
        fixture.ChangeSignal.Setup(signal => signal.TrySignal()).Returns(false);

        CaptureId? captureId = fixture.Service.TryFinalize(
            @"C:\CaptureTool\Captures\capture.png",
            CaptureFileType.Image);

        Assert.IsNotNull(captureId);
        fixture.ChangeSignal.Verify(signal => signal.TrySignal(), Times.Once);
    }

    [TestMethod]
    public void TrySetPreferredOpenPath_ShouldPreserveIdentityAndRetainedSource()
    {
        LifecycleFixture fixture = new();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        string preferredPath = @"D:\Pictures\capture.png";
        CaptureAsset asset = new(
            CaptureId.New(),
            CaptureFileType.Image,
            retainedPath,
            CaptureSourceOwnership.AppOwned,
            new DateTimeOffset(UtcNow));
        fixture.AssetCatalog.Setup(catalog => catalog.Get(asset.Id)).Returns(asset);
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryUpdate(
                It.IsAny<CaptureAsset>(),
                It.IsAny<long>(),
                It.IsAny<CaptureAssetChangeType>()))
            .Returns<CaptureAsset, long, CaptureAssetChangeType>((updated, _, _) =>
                CaptureAssetCatalogWriteResult.Committed(updated, 2));
        fixture.RecentCatalog
            .Setup(catalog => catalog.TryProjectCaptured(
                It.IsAny<string>(),
                It.IsAny<CaptureFileType>(),
                It.IsAny<CaptureId>(),
                It.IsAny<long>(),
                It.IsAny<DateTime>()))
            .Returns(true);

        fixture.Service.TrySetPreferredOpenPath(asset.Id, retainedPath, preferredPath);

        fixture.AssetCatalog.Verify(catalog => catalog.TryUpdate(
            It.Is<CaptureAsset>(updated =>
                updated.Id == asset.Id &&
                updated.RetainedSourcePath == retainedPath &&
                updated.PreferredOpenPath == preferredPath &&
                updated.LifecycleRevision == 2),
            1,
            CaptureAssetChangeType.PreferredLocationChanged), Times.Once);
        fixture.RecentCatalog.Verify(catalog => catalog.TryProjectCaptured(
            preferredPath,
            CaptureFileType.Image,
            asset.Id,
            2,
            UtcNow), Times.Once);
    }

    [TestMethod]
    public void TrySetPreferredOpenPath_WhenFinalizationHasNoIdentity_ShouldKeepRetainedFallbackForRepair()
    {
        LifecycleFixture fixture = new();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        string preferredPath = @"D:\Pictures\capture.png";
        fixture.AssetCatalog
            .Setup(catalog => catalog.TryAdd(It.IsAny<CaptureAsset>()))
            .Returns(CaptureAssetCatalogWriteResult.Failed);

        CaptureId? captureId = fixture.Service.TryFinalize(retainedPath, CaptureFileType.Image);
        fixture.Service.TrySetPreferredOpenPath(captureId, retainedPath, preferredPath);

        Assert.IsNull(captureId);
        fixture.RecentCatalog.Verify(
            catalog => catalog.RecordCaptured(retainedPath, CaptureFileType.Image),
            Times.Once);
        fixture.RecentCatalog.Verify(
            catalog => catalog.ReplacePath(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public void TrySetPreferredOpenPath_WhenAssetUpdateFails_ShouldStillUpdateRecentPath()
    {
        LifecycleFixture fixture = new();
        CaptureId captureId = CaptureId.New();
        string retainedPath = @"C:\CaptureTool\Captures\capture.png";
        string preferredPath = @"D:\Pictures\capture.png";
        fixture.AssetCatalog.Setup(catalog => catalog.Get(captureId)).Returns((CaptureAsset?)null);

        fixture.Service.TrySetPreferredOpenPath(captureId, retainedPath, preferredPath);

        fixture.RecentCatalog.Verify(catalog => catalog.TryRepairCapturedProjection(
            retainedPath,
            preferredPath,
            CaptureFileType.Image,
            captureId,
            UtcNow), Times.Once);
    }

    private sealed class LifecycleFixture
    {
        public LifecycleFixture()
        {
            Clock.SetupGet(clock => clock.UtcNow).Returns(UtcNow);
            ChangeSignal.Setup(signal => signal.TrySignal()).Returns(true);
            Service = new(
                AssetCatalog.Object,
                RecentCatalog.Object,
                ChangeSignal.Object,
                Clock.Object,
                LogService.Object);
        }

        public CaptureAssetLifecycleService Service { get; }
        public Mock<ICaptureAssetCatalog> AssetCatalog { get; } = new();
        public Mock<IRecentCaptureCatalog> RecentCatalog { get; } = new();
        public Mock<ICaptureAssetChangeSignal> ChangeSignal { get; } = new();
        public Mock<IClock> Clock { get; } = new();
        public Mock<ILogService> LogService { get; } = new();
    }
}
