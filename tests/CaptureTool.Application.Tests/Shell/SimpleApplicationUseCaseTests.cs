using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Abstractions.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Edit.Audio.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Edit.Video.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using CaptureTool.Application.Abstractions.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Shell.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Shell.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Capture.Audio;
using CaptureTool.Application.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Capture.Audio.OpenAudioCapturePage;
using CaptureTool.Application.Capture.Audio.PauseAudioCapture;
using CaptureTool.Application.Capture.Audio.StartAudioCapture;
using CaptureTool.Application.Capture.Audio.StopAudioCapture;
using CaptureTool.Application.Capture.Audio.ToggleLocalAudioCapture;
using CaptureTool.Application.Edit.Image.OpenImageEditPage;
using CaptureTool.Application.Library.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Settings.OpenSettingsPage;
using CaptureTool.Application.Shell.About.LeaveAboutPage;
using CaptureTool.Application.Shell.About.OpenAboutPage;
using CaptureTool.Application.Shell.AppMenu.ExitApplication;
using CaptureTool.Application.Shell.Error.RestartApplication;
using CaptureTool.Application.Shell.Home.ShowHomePage;
using CaptureTool.Application.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Store.LeaveStorePage;
using CaptureTool.Application.Store.OpenStorePage;
using CaptureTool.Application.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Tests.Capture.Audio;
using CaptureTool.Domain.FileSystem;
using Moq;

namespace CaptureTool.Application.Tests.Shell;

[TestClass]
public sealed class SimpleApplicationUseCaseTests
{
    [TestMethod]
    public async Task NavigationUseCases_NavigateToExpectedRoutes()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(service => service.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var imageFile = new ImageFile("capture.png");
        var audioCaptureNavigationGuard = new AllowAudioCaptureNavigationGuard();
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(
            navigation.Object,
            editGuard.Object,
            audioCaptureNavigationGuard);

        await new OpenAboutPageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenAboutPageRequest(), TestContext.CancellationToken);
        await new ShowHomePageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new ShowHomePageRequest(), TestContext.CancellationToken);
        await new OpenStorePageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenStorePageRequest(), TestContext.CancellationToken);
        await new OpenSettingsPageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenSettingsPageRequest(), TestContext.CancellationToken);
        await new OpenAudioCapturePageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenAudioCapturePageRequest(), TestContext.CancellationToken);
        await new OpenImageEditPageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenImageEditPageRequest(imageFile), TestContext.CancellationToken);

        navigation.Verify(service => service.NavigateAsync(NavigationRoute.About, null, false, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.Home, null, true, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.Store, null, false, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.Settings, null, false, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.AudioCapture, null, false, It.IsAny<CancellationToken>()), Times.Once);
        navigation.Verify(service => service.NavigateAsync(NavigationRoute.ImageEdit, imageFile, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task LeavePageUseCases_WhenBackFails_NavigateHomeAndClearHistory()
    {
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        navigation
            .Setup(service => service.TryGoBackAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.NoChange);
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(navigation.Object);

        await new LeaveAboutPageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new LeaveAboutPageRequest(), TestContext.CancellationToken);
        await new LeaveStorePageUseCase(coordinator, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new LeaveStorePageRequest(), TestContext.CancellationToken);

        navigation.Verify(
            service => service.NavigateAsync(NavigationRoute.Home, null, true, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task ShutdownUseCases_RespectShutdownStateAndInvokeHandler()
    {
        var shutdown = new Mock<IShutdownHandler>();
        shutdown.Setup(handler => handler.TryRestart()).Returns(true);
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(Mock.Of<INavigationService>());
        var exit = new ExitApplicationUseCase(shutdown.Object, coordinator, TestUseCaseExecutor.Instance);
        var restart = new RestartApplicationUseCase(shutdown.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(exit.CanExecute(new ExitApplicationRequest()));
        Assert.IsTrue(restart.CanExecute(new RestartApplicationRequest()));

        ExitApplicationResponse exitResponse = (await exit.ExecuteAsync(new ExitApplicationRequest(), TestContext.CancellationToken)).Value!;
        RestartApplicationResponse restartResponse = (await restart.ExecuteAsync(new RestartApplicationRequest(), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(exitResponse.Succeeded);
        Assert.IsTrue(restartResponse.Succeeded);
        shutdown.Verify(handler => handler.Shutdown(), Times.Once);
        shutdown.Verify(handler => handler.TryRestart(), Times.Once);

        shutdown.Setup(handler => handler.IsShuttingDown).Returns(true);
        Assert.IsFalse(exit.CanExecute(new ExitApplicationRequest()));
        Assert.IsFalse(restart.CanExecute(new RestartApplicationRequest()));
    }

    [TestMethod]
    public async Task RestartApplicationUseCase_WhenRestartFails_ReturnsFailure()
    {
        var shutdown = new Mock<IShutdownHandler>();
        shutdown.Setup(handler => handler.TryRestart()).Returns(false);
        var restart = new RestartApplicationUseCase(shutdown.Object, TestUseCaseExecutor.Instance);

        RestartApplicationResponse response = (await restart.ExecuteAsync(
            new RestartApplicationRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
    }

    [TestMethod]
    public async Task ExitApplicationUseCase_WhenLeavePolicyRejects_DoesNotShutdown()
    {
        var shutdown = new Mock<IShutdownHandler>();
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(guard => guard.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(
            Mock.Of<INavigationService>(),
            editGuard.Object);
        var exit = new ExitApplicationUseCase(
            shutdown.Object,
            coordinator,
            TestUseCaseExecutor.Instance);

        ExitApplicationResponse response = (await exit.ExecuteAsync(
            new ExitApplicationRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        shutdown.Verify(handler => handler.Shutdown(), Times.Never);
    }

    [TestMethod]
    public async Task AudioCaptureUseCases_InvokeAudioCaptureWorkflow()
    {
        var audioCapture = new FakeAudioCaptureWorkflow();
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var audioFile = new AudioFile("capture.wav");
        audioCapture.AudioFile = audioFile;

        await new StartAudioCaptureUseCase(audioCapture, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new StartAudioCaptureRequest(), TestContext.CancellationToken);
        await new PauseAudioCaptureUseCase(audioCapture, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new PauseAudioCaptureRequest(), TestContext.CancellationToken);
        await new StopAudioCaptureUseCase(audioCapture, navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new StopAudioCaptureRequest(), TestContext.CancellationToken);
        await new ToggleLocalAudioCaptureUseCase(audioCapture, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new ToggleLocalAudioCaptureRequest(), TestContext.CancellationToken);

        Assert.AreEqual(1, audioCapture.StartCallCount);
        Assert.AreEqual(1, audioCapture.PauseCallCount);
        Assert.AreEqual(1, audioCapture.StopCallCount);
        Assert.AreEqual(1, audioCapture.ToggleLocalAudioCallCount);
        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.AudioEdit,
                audioFile,
                false,
                TestContext.CancellationToken),
            Times.Once);
    }

    [TestMethod]
    public async Task StopAudioCaptureUseCase_WhenHostRejects_ReportsFailure()
    {
        var audioCapture = new FakeAudioCaptureWorkflow
        {
            AudioFile = new AudioFile("capture.wav")
        };
        var navigation = new Mock<INavigationService>();
        navigation
            .Setup(service => service.NavigateAsync(
                NavigationRoute.AudioEdit,
                audioCapture.AudioFile,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(NavigationResult.Rejected);
        var useCase = new StopAudioCaptureUseCase(
            audioCapture,
            navigation.Object,
            TestUseCaseExecutor.Instance);

        StopAudioCaptureResponse response = (await useCase.ExecuteAsync(
            new StopAudioCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        Assert.AreEqual(1, audioCapture.StopCallCount);
    }

    [TestMethod]
    public async Task CancelAudioCaptureUseCase_WhenWarningsDisabled_CancelsWithoutPrompt()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard))
            .Returns(false);
        var useCase = new CancelAudioCaptureUseCase(
            audioCapture,
            confirmationService.Object,
            settings.Object,
            TestUseCaseExecutor.Instance);

        CancelAudioCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelAudioCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, audioCapture.CancelCallCount);
        confirmationService.Verify(
            service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task CancelAudioCaptureUseCase_WhenWarningsEnabledAndUserConfirms_Cancels()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard))
            .Returns(true);
        var useCase = new CancelAudioCaptureUseCase(
            audioCapture,
            confirmationService.Object,
            settings.Object,
            TestUseCaseExecutor.Instance);

        CancelAudioCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelAudioCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsTrue(response.Succeeded);
        Assert.AreEqual(1, audioCapture.CancelCallCount);
    }

    [TestMethod]
    public async Task CancelAudioCaptureUseCase_WhenWarningsEnabledAndUserDeclines_DoesNotCancel()
    {
        var audioCapture = new FakeAudioCaptureWorkflow { IsRecording = true };
        var confirmationService = new Mock<ICaptureDiscardConfirmationService>();
        confirmationService
            .Setup(service => service.ConfirmDiscardActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard))
            .Returns(true);
        var useCase = new CancelAudioCaptureUseCase(
            audioCapture,
            confirmationService.Object,
            settings.Object,
            TestUseCaseExecutor.Instance);

        CancelAudioCaptureResponse response = (await useCase.ExecuteAsync(
            new CancelAudioCaptureRequest(),
            TestContext.CancellationToken)).Value!;

        Assert.IsFalse(response.Succeeded);
        Assert.AreEqual(0, audioCapture.CancelCallCount);
    }

    [TestMethod]
    public async Task StoreUseCases_QueryAndPurchaseChromaKeyAddOn()
    {
        var addOn = Mock.Of<IStoreAddOn>(addon => addon.Id == CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval);
        var store = new Mock<IStoreService>();
        store
            .Setup(service => service.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, TestContext.CancellationToken))
            .ReturnsAsync(addOn);
        store
            .Setup(service => service.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, TestContext.CancellationToken))
            .ReturnsAsync(true);

        var getAddOn = new GetChromaKeyAddOnUseCase(store.Object, TestUseCaseExecutor.Instance);
        var purchase = new PurchaseChromaKeyAddOnUseCase(store.Object, TestUseCaseExecutor.Instance);

        Assert.IsTrue(getAddOn.CanExecute(new GetChromaKeyAddOnRequest()));
        Assert.IsTrue(purchase.CanExecute(new PurchaseChromaKeyAddOnRequest()));
        GetChromaKeyAddOnResponse getResponse = (await getAddOn.ExecuteAsync(new GetChromaKeyAddOnRequest(), TestContext.CancellationToken)).Value!;
        PurchaseChromaKeyAddOnResponse purchaseResponse = (await purchase.ExecuteAsync(new PurchaseChromaKeyAddOnRequest(), TestContext.CancellationToken)).Value!;

        Assert.AreSame(addOn, getResponse.AddOn);
        Assert.IsTrue(purchaseResponse.Purchased);
    }

    [TestMethod]
    public async Task OpenRecentCaptureUseCase_RoutesByDetectedFileType()
    {
        string audioPath = await CreateTempFileAsync("capture.wav");
        string imagePath = await CreateTempFileAsync("capture.png");
        string videoPath = await CreateTempFileAsync("capture.mp4");
        var audioEdit = new Mock<IOpenAudioEditPageUseCase>();
        var imageEdit = new Mock<IOpenImageEditPageUseCase>();
        var videoEdit = new Mock<IOpenVideoEditPageUseCase>();
        audioEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenAudioEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenAudioEditPageResponse>.Success(new OpenAudioEditPageResponse()));
        imageEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenImageEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenImageEditPageResponse>.Success(new OpenImageEditPageResponse()));
        videoEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenVideoEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenVideoEditPageResponse>.Success(new OpenVideoEditPageResponse()));
        var catalog = new Mock<IRecentCaptureCatalog>();
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(Mock.Of<INavigationService>());
        var useCase = new OpenRecentCaptureUseCase(
            TestFileSystem.Instance,
            CreateRecentCaptureStorage(),
            catalog.Object,
            audioEdit.Object,
            imageEdit.Object,
            videoEdit.Object,
            coordinator,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new OpenRecentCaptureRequest(audioPath)));
        OpenRecentCaptureResponse audioResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(audioPath), TestContext.CancellationToken)).Value!;
        OpenRecentCaptureResponse imageResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(imagePath), TestContext.CancellationToken)).Value!;
        OpenRecentCaptureResponse videoResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(videoPath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(audioResponse.Opened);
        Assert.IsTrue(imageResponse.Opened);
        Assert.IsTrue(videoResponse.Opened);
        audioEdit.Verify(useCase => useCase.ExecuteAsync(
            It.Is<OpenAudioEditPageRequest>(request =>
                request.AudioFile.FilePath != audioPath &&
                Path.GetExtension(request.AudioFile.FilePath) == ".wav"),
            TestContext.CancellationToken), Times.Once);
        imageEdit.Verify(useCase => useCase.ExecuteAsync(
            It.Is<OpenImageEditPageRequest>(request =>
                request.ImageFile.FilePath != imagePath &&
                request.ImageFile.PersistentFilePath == imagePath),
            TestContext.CancellationToken), Times.Once);
        videoEdit.Verify(useCase => useCase.ExecuteAsync(
            It.Is<OpenVideoEditPageRequest>(request =>
                request.VideoFile.FilePath != videoPath &&
                Path.GetExtension(request.VideoFile.FilePath) == ".mp4"),
            TestContext.CancellationToken), Times.Once);
        catalog.Verify(value => value.Touch(It.IsAny<string>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task OpenRecentCaptureUseCase_ReturnsNotOpenedForMissingOrUnknownFiles()
    {
        string unknownPath = await CreateTempFileAsync("capture.bin");
        INavigationCoordinator coordinator = TestNavigationCoordinator.Create(Mock.Of<INavigationService>());
        var useCase = new OpenRecentCaptureUseCase(
            TestFileSystem.Instance,
            CreateRecentCaptureStorage(),
            Mock.Of<IRecentCaptureCatalog>(),
            Mock.Of<IOpenAudioEditPageUseCase>(),
            Mock.Of<IOpenImageEditPageUseCase>(),
            Mock.Of<IOpenVideoEditPageUseCase>(),
            coordinator,
            TestUseCaseExecutor.Instance);

        Assert.IsFalse(useCase.CanExecute(new OpenRecentCaptureRequest("")));
        OpenRecentCaptureResponse missingResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(@"C:\missing.png"), TestContext.CancellationToken)).Value!;
        OpenRecentCaptureResponse unknownResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(unknownPath), TestContext.CancellationToken)).Value!;

        Assert.IsFalse(missingResponse.Opened);
        Assert.IsFalse(unknownResponse.Opened);
    }

    private static async Task<string> CreateTempFileAsync(string fileName)
    {
        string path = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString(), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "capture");
        return path;
    }

    private static IStorageService CreateRecentCaptureStorage()
    {
        var storage = new Mock<IStorageService>();
        storage.Setup(service => service.GetApplicationTemporaryFolderPath())
            .Returns(Path.Combine(Path.GetTempPath(), "CaptureToolTests", "RecentWorkingFiles"));
        storage.Setup(service => service.GetTemporaryFileName()).Returns(Guid.NewGuid().ToString());
        return storage.Object;
    }

    public TestContext TestContext { get; set; } = null!;
}
