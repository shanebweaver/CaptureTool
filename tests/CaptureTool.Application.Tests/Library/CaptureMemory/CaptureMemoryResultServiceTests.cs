using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Library.CaptureMemory;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Library.CaptureMemory;

[TestClass]
public sealed class CaptureMemoryResultServiceTests
{
    [TestMethod]
    public async Task ResolveAsync_PrefersCurrentPreferredPath()
    {
        CaptureAsset asset = CreateAsset(preferredPath: @"C:\Users\test\Pictures\shared.png");
        var catalog = new Mock<ICaptureAssetCatalog>();
        catalog.Setup(value => value.Get(asset.Id)).Returns(asset);
        var files = new Mock<IFileSystem>();
        files.Setup(value => value.FileExists(asset.PreferredOpenPath!)).Returns(true);
        var resolver = new CaptureMemoryResultResolver(catalog.Object, files.Object);

        CaptureMemoryResultLocation result = await resolver.ResolveAsync(asset.Id);

        Assert.AreEqual(CaptureMemoryResultLocationStatus.Available, result.Status);
        Assert.AreEqual(asset.PreferredOpenPath, result.CurrentFilePath);
        Assert.AreEqual("shared.png", result.DisplayFileName);
    }

    [TestMethod]
    public async Task ResolveAsync_FallsBackToRetainedSourceAndReportsMissingWithoutAPath()
    {
        CaptureAsset asset = CreateAsset(preferredPath: @"C:\Users\test\Pictures\moved.png");
        var catalog = new Mock<ICaptureAssetCatalog>();
        catalog.Setup(value => value.Get(asset.Id)).Returns(asset);
        var files = new Mock<IFileSystem>();
        files.Setup(value => value.FileExists(asset.PreferredOpenPath!)).Returns(false);
        files.SetupSequence(value => value.FileExists(asset.RetainedSourcePath))
            .Returns(true)
            .Returns(false);
        var resolver = new CaptureMemoryResultResolver(catalog.Object, files.Object);

        CaptureMemoryResultLocation fallback = await resolver.ResolveAsync(asset.Id);
        CaptureMemoryResultLocation missing = await resolver.ResolveAsync(asset.Id);

        Assert.AreEqual(asset.RetainedSourcePath, fallback.CurrentFilePath);
        Assert.AreEqual(CaptureMemoryResultLocationStatus.SourceMissing, missing.Status);
        Assert.IsNull(missing.CurrentFilePath);
    }

    [TestMethod]
    public async Task ResolveAsync_DoesNotExposeAPathForForgottenCapture()
    {
        CaptureAsset deleted = CreateAsset().MarkDeleted();
        var catalog = new Mock<ICaptureAssetCatalog>();
        catalog.Setup(value => value.Get(deleted.Id)).Returns(deleted);
        var resolver = new CaptureMemoryResultResolver(catalog.Object, Mock.Of<IFileSystem>());

        CaptureMemoryResultLocation result = await resolver.ResolveAsync(deleted.Id);

        Assert.AreEqual(CaptureMemoryResultLocationStatus.Forgotten, result.Status);
        Assert.IsNull(result.CurrentFilePath);
    }

    [TestMethod]
    public async Task ResolveAsync_ReportsUnavailableWhenTheFileSystemCannotResolveTheSource()
    {
        CaptureAsset asset = CreateAsset();
        var catalog = new Mock<ICaptureAssetCatalog>();
        catalog.Setup(value => value.Get(asset.Id)).Returns(asset);
        var files = new Mock<IFileSystem>();
        files.Setup(value => value.FileExists(asset.RetainedSourcePath))
            .Throws(new IOException("source lookup failed"));
        var resolver = new CaptureMemoryResultResolver(catalog.Object, files.Object);

        CaptureMemoryResultLocation result = await resolver.ResolveAsync(asset.Id);

        Assert.AreEqual(CaptureMemoryResultLocationStatus.Unavailable, result.Status);
        Assert.IsNull(result.CurrentFilePath);
    }

    [TestMethod]
    public async Task OpenAsync_ResolvesTheCurrentPathAtInvocationTime()
    {
        CaptureId captureId = CaptureId.New();
        string currentPath = @"C:\Users\test\Pictures\current.png";
        var resolver = new Mock<ICaptureMemoryResultResolver>();
        resolver.Setup(value => value.ResolveAsync(captureId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaptureMemoryResultLocation(
                captureId,
                CaptureMemoryResultLocationStatus.Available,
                "current.png",
                currentPath));
        var opener = new Mock<IOpenRecentCaptureUseCase>();
        opener.Setup(value => value.ExecuteAsync(
                It.Is<OpenRecentCaptureRequest>(request => request.FilePath == currentPath),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptureTool.Application.Abstractions.UseCases.UseCaseResponse<OpenRecentCaptureResponse>
                .Success(new OpenRecentCaptureResponse()));
        var executor = new UseCaseExecutor(Mock.Of<ILogService>(), Mock.Of<ITelemetryService>());
        var useCase = new OpenCaptureMemoryResultUseCase(resolver.Object, opener.Object, executor);

        var result = await useCase.ExecuteAsync(new OpenCaptureMemoryResultRequest(captureId));

        Assert.AreEqual(OpenCaptureMemoryResultStatus.Opened, result.Value?.Status);
        resolver.VerifyAll();
        opener.VerifyAll();
    }

    private static CaptureAsset CreateAsset(string? preferredPath = null)
    {
        return new CaptureAsset(
            CaptureId.New(),
            CaptureFileType.Image,
            @"C:\Users\test\AppData\Local\CaptureTool\capture.png",
            CaptureSourceOwnership.AppOwned,
            DateTimeOffset.UtcNow,
            preferredPath);
    }
}
