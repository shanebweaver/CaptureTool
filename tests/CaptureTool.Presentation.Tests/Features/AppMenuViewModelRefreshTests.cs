using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.GetRecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shell.AppMenu.OpenFile;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Store.OpenStorePage;
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
        var recentCapture = new RecentCapture(@"C:\Temp\source.png", "source.png", CaptureFileType.Image);

        openFileUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenFileResponse>.Success(new OpenFileResponse()));
        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<GetRecentCapturesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<GetRecentCapturesResponse>.Success(new GetRecentCapturesResponse([recentCapture])));
        recentCaptureFactory
            .Setup(factory => factory.Create(recentCapture.FilePath))
            .Returns(new RecentCaptureViewModel(recentCapture.FilePath));

        var viewModel = new AppMenuViewModel(
            Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            Mock.Of<IOpenSettingsPageUseCase>(),
            Mock.Of<IOpenAboutPageUseCase>(),
            Mock.Of<IOpenStorePageUseCase>(),
            Mock.Of<IShowHomePageUseCase>(),
            openFileUseCase.Object,
            Mock.Of<IExitApplicationUseCase>(),
            Mock.Of<IOpenRecentCaptureUseCase>(),
            getRecentCapturesUseCase.Object,
            Mock.Of<IStoreFeatureAvailability>(),
            Mock.Of<IImageCaptureState>(),
            Mock.Of<IVideoCaptureState>(),
            Mock.Of<IAudioCaptureState>(),
            recentCaptureFactory.Object,
            Mock.Of<IRecentCapturesChangeNotifier>());

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
    public async Task NavigateHomeCommand_ShouldShowHomePage()
    {
        var showHomePageUseCase = new Mock<IShowHomePageUseCase>();
        showHomePageUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<ShowHomePageRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ShowHomePageResponse>.Success(new ShowHomePageResponse()));
        var viewModel = CreateViewModel(showHomePageUseCase: showHomePageUseCase.Object);

        viewModel.NavigateHomeCommand.Execute(null);
        await viewModel.NavigateHomeCommand.ExecutionTask!;

        showHomePageUseCase.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<ShowHomePageRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task NavigateHomeCommand_WhenEditSessionGuardBlocks_DoesNotShowHomePage()
    {
        var showHomePageUseCase = new Mock<IShowHomePageUseCase>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            showHomePageUseCase: showHomePageUseCase.Object,
            editSessionGuard: editSessionGuard.Object);

        viewModel.NavigateHomeCommand.Execute(null);
        await viewModel.NavigateHomeCommand.ExecutionTask!;

        showHomePageUseCase.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<ShowHomePageRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public void RecentCapturesChanged_WhenMenuIsLoaded_ShouldRefreshRemovedFiles()
    {
        var notifier = new Mock<IRecentCapturesChangeNotifier>();
        var getRecentCapturesUseCase = new Mock<IGetRecentCapturesUseCase>();
        var recentCaptureFactory = new Mock<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>();
        var recentCapture = new RecentCapture(@"C:\Temp\source.png", "source.png", CaptureFileType.Image);
        int refreshCount = 0;

        getRecentCapturesUseCase
            .Setup(useCase => useCase.ExecuteAsync(
                It.IsAny<GetRecentCapturesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => UseCaseResponse<GetRecentCapturesResponse>.Success(
                new GetRecentCapturesResponse(refreshCount++ == 0 ? [recentCapture] : [])));
        recentCaptureFactory
            .Setup(factory => factory.Create(recentCapture.FilePath))
            .Returns(new RecentCaptureViewModel(recentCapture.FilePath));

        AppMenuViewModel viewModel = CreateViewModel(
            getRecentCapturesUseCase: getRecentCapturesUseCase.Object,
            recentCaptureViewModelFactory: recentCaptureFactory.Object,
            recentCapturesChangeNotifier: notifier.Object);
        viewModel.Load();
        Assert.HasCount(1, viewModel.RecentCaptures);

        notifier.Raise(source => source.RecentCapturesChanged += null, EventArgs.Empty);

        Assert.IsEmpty(viewModel.RecentCaptures);
        getRecentCapturesUseCase.Verify(
            useCase => useCase.ExecuteAsync(
                It.IsAny<GetRecentCapturesRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        viewModel.Dispose();
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
        var openRecentCaptureUseCase = new Mock<IOpenRecentCaptureUseCase>();
        var editSessionGuard = new Mock<IEditSessionGuard>();
        editSessionGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var viewModel = CreateViewModel(
            openRecentCaptureUseCase: openRecentCaptureUseCase.Object,
            editSessionGuard: editSessionGuard.Object);
        var recentCapture = new RecentCaptureViewModel(filePath);

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
        IShowHomePageUseCase? showHomePageUseCase = null,
        IEditSessionGuard? editSessionGuard = null,
        IGetRecentCapturesUseCase? getRecentCapturesUseCase = null,
        IFactoryServiceWithArgs<RecentCaptureViewModel, string>? recentCaptureViewModelFactory = null,
        IRecentCapturesChangeNotifier? recentCapturesChangeNotifier = null)
    {
        return new AppMenuViewModel(
            openSelectionOverlayUseCase ?? Mock.Of<IOpenSelectionOverlayUseCase>(),
            Mock.Of<IOpenAudioCapturePageUseCase>(),
            Mock.Of<IOpenSettingsPageUseCase>(),
            Mock.Of<IOpenAboutPageUseCase>(),
            Mock.Of<IOpenStorePageUseCase>(),
            showHomePageUseCase ?? Mock.Of<IShowHomePageUseCase>(),
            openFileUseCase ?? Mock.Of<IOpenFileUseCase>(),
            Mock.Of<IExitApplicationUseCase>(),
            openRecentCaptureUseCase ?? Mock.Of<IOpenRecentCaptureUseCase>(),
            getRecentCapturesUseCase ?? Mock.Of<IGetRecentCapturesUseCase>(),
            Mock.Of<IStoreFeatureAvailability>(),
            Mock.Of<IImageCaptureState>(),
            Mock.Of<IVideoCaptureState>(),
            Mock.Of<IAudioCaptureState>(),
            recentCaptureViewModelFactory ?? Mock.Of<IFactoryServiceWithArgs<RecentCaptureViewModel, string>>(),
            recentCapturesChangeNotifier ?? Mock.Of<IRecentCapturesChangeNotifier>(),
            editSessionGuard);
    }

    public TestContext TestContext { get; set; } = null!;
}
