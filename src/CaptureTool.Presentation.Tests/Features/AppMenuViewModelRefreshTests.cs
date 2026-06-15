using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Features.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.RecentCaptures;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.RecentCaptures;
using CaptureTool.Presentation.Shell;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class AppMenuViewModelRefreshTests
{
    [TestMethod]
    public async Task OpenFileCommand_ShouldRefreshRecentCaptures_AfterFileOpens()
    {
        var openFileUseCase = new Mock<IOpenFileUseCase>();
        var getRecentCapturesUseCase = new Mock<IGetRecentCapturesUseCase>();
        var recentCaptureFactory = new Mock<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>();
        var fileTypeDetector = new Mock<IFileTypeDetector>();
        var recentCapture = new RecentCapture(@"C:\Temp\source.png", "source.png", CaptureFileType.Image);

        openFileUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OpenFileResponse());
        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetRecentCapturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetRecentCapturesResponse([recentCapture]));
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(recentCapture.FilePath))
            .Returns(CaptureFileType.Image);
        recentCaptureFactory
            .Setup(factory => factory.Create(recentCapture.FilePath))
            .Returns(new RecentCaptureViewModel(recentCapture.FilePath, fileTypeDetector.Object));

        var viewModel = new AppMenuViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenSettingsPageUseCase>(),
            Mock.Of<IOpenAboutPageUseCase>(),
            Mock.Of<IOpenStorePageUseCase>(),
            openFileUseCase.Object,
            Mock.Of<IExitApplicationUseCase>(),
            Mock.Of<IOpenRecentCaptureUseCase>(),
            getRecentCapturesUseCase.Object,
            Mock.Of<IStoreFeatureAvailability>(),
            Mock.Of<IImageCaptureHandler>(),
            Mock.Of<IVideoCaptureHandler>(),
            recentCaptureFactory.Object);

        viewModel.OpenFileCommand.Execute(null);
        await viewModel.OpenFileCommand.ExecutionTask!;

        openFileUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenFileRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        getRecentCapturesUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<GetRecentCapturesRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.HasCount(1, viewModel.RecentCaptures);
        Assert.AreEqual(recentCapture.FilePath, viewModel.RecentCaptures[0].FilePath);
    }
}
