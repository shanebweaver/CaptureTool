using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.AudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
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
using CaptureTool.Application.Abstractions.UseCases;
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
            .ReturnsAsync(UseCaseResponse<OpenFileResponse>.Success(new OpenFileResponse()));
        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetRecentCapturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetRecentCapturesResponse>.Success(new GetRecentCapturesResponse([recentCapture])));
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(recentCapture.FilePath))
            .Returns(CaptureFileType.Image);
        recentCaptureFactory
            .Setup(factory => factory.Create(recentCapture.FilePath))
            .Returns(new RecentCaptureViewModel(recentCapture.FilePath, fileTypeDetector.Object));

        var viewModel = new AppMenuViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            Mock.Of<IOpenSettingsPageUseCase>(),
            Mock.Of<IOpenAboutPageUseCase>(),
            Mock.Of<IOpenStorePageUseCase>(),
            openFileUseCase.Object,
            Mock.Of<IExitApplicationUseCase>(),
            Mock.Of<IOpenRecentCaptureUseCase>(),
            getRecentCapturesUseCase.Object,
            Mock.Of<IAudioCaptureFeatureAvailability>(),
            Mock.Of<IStoreFeatureAvailability>(),
            Mock.Of<IImageCaptureHandler>(),
            Mock.Of<IVideoCaptureHandler>(),
            Mock.Of<IAudioCaptureHandler>(),
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

    [TestMethod]
    public async Task NewImageCaptureCommand_WhenEditSessionGuardBlocks_DoesNotStartCapture()
    {
        var openSelectionOverlayUseCase = new Mock<IOpenSelectionOverlayUseCase>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            openSelectionOverlayUseCase: openSelectionOverlayUseCase.Object,
            editSessionGuard: editSessionGuard.Object);

        viewModel.NewImageCaptureCommand.Execute(null);
        await viewModel.NewImageCaptureCommand.ExecutionTask!;

        openSelectionOverlayUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OpenFileCommand_WhenEditSessionGuardBlocks_DoesNotOpenFile()
    {
        var openFileUseCase = new Mock<IOpenFileUseCase>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            openFileUseCase: openFileUseCase.Object,
            editSessionGuard: editSessionGuard.Object);

        viewModel.OpenFileCommand.Execute(null);
        await viewModel.OpenFileCommand.ExecutionTask!;

        openFileUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenFileRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task OpenRecentCaptureCommand_WhenEditSessionGuardBlocks_DoesNotOpenRecentCapture()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString(), "capture.png");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, "capture", TestContext.CancellationToken);
        var fileTypeDetector = new Mock<IFileTypeDetector>();
        var openRecentCaptureUseCase = new Mock<IOpenRecentCaptureUseCase>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        fileTypeDetector
            .Setup(detector => detector.DetectFileType(filePath))
            .Returns(CaptureFileType.Image);
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            openRecentCaptureUseCase: openRecentCaptureUseCase.Object,
            editSessionGuard: editSessionGuard.Object);
        var recentCapture = new RecentCaptureViewModel(filePath, fileTypeDetector.Object);

        viewModel.OpenRecentCaptureCommand.Execute(recentCapture);
        await viewModel.OpenRecentCaptureCommand.ExecutionTask!;

        openRecentCaptureUseCase.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenRecentCaptureRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AppMenuViewModel CreateViewModel(
        IOpenSelectionOverlayUseCase? openSelectionOverlayUseCase = null,
        IOpenFileUseCase? openFileUseCase = null,
        IOpenRecentCaptureUseCase? openRecentCaptureUseCase = null,
        IEditSessionGuard? editSessionGuard = null)
    {
        return new AppMenuViewModel(
            openSelectionOverlayUseCase ?? Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            Mock.Of<IOpenSettingsPageUseCase>(),
            Mock.Of<IOpenAboutPageUseCase>(),
            Mock.Of<IOpenStorePageUseCase>(),
            openFileUseCase ?? Mock.Of<IOpenFileUseCase>(),
            Mock.Of<IExitApplicationUseCase>(),
            openRecentCaptureUseCase ?? Mock.Of<IOpenRecentCaptureUseCase>(),
            Mock.Of<IGetRecentCapturesUseCase>(),
            Mock.Of<IAudioCaptureFeatureAvailability>(),
            Mock.Of<IStoreFeatureAvailability>(),
            Mock.Of<IImageCaptureHandler>(),
            Mock.Of<IVideoCaptureHandler>(),
            Mock.Of<IAudioCaptureHandler>(),
            Mock.Of<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>(),
            editSessionGuard);
    }

    public TestContext TestContext { get; set; } = null!;
}
