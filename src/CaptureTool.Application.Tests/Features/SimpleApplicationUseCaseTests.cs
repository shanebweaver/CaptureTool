using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Features.About.LeaveAboutPage;
using CaptureTool.Application.Abstractions.Features.About.OpenAboutPage;
using CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Abstractions.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Abstractions.Features.AudioEdit.OpenAudioEditPage;
using CaptureTool.Application.Abstractions.Features.Error.RestartApplication;
using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Abstractions.Features.Navigation;
using CaptureTool.Application.Abstractions.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Abstractions.Features.Settings.OpenSettingsPage;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Features.Store.LeaveStorePage;
using CaptureTool.Application.Abstractions.Features.Store.OpenStorePage;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Features.VideoEdit.OpenVideoEditPage;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.About.LeaveAboutPage;
using CaptureTool.Application.Features.About.OpenAboutPage;
using CaptureTool.Application.Features.AppMenu.ExitApplication;
using CaptureTool.Application.Features.AudioCapture.OpenAudioCapturePage;
using CaptureTool.Application.Features.AudioCapture.PauseAudioCapture;
using CaptureTool.Application.Features.AudioCapture.StartAudioCapture;
using CaptureTool.Application.Features.AudioCapture.StopAudioCapture;
using CaptureTool.Application.Features.AudioCapture.ToggleLocalAudioCapture;
using CaptureTool.Application.Features.Error.RestartApplication;
using CaptureTool.Application.Features.Home.ShowHomePage;
using CaptureTool.Application.Features.ImageEdit.OpenImageEditPage;
using CaptureTool.Application.Features.RecentCaptures.OpenRecentCapture;
using CaptureTool.Application.Features.SettingsPage.OpenSettingsPage;
using CaptureTool.Application.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Features.Store.LeaveStorePage;
using CaptureTool.Application.Features.Store.OpenStorePage;
using CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Domain.Capture;
using CaptureTool.Domain.Capture.Files;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class SimpleApplicationUseCaseTests
{
    [TestMethod]
    public async Task NavigationUseCases_NavigateToExpectedRoutes()
    {
        var navigation = new Mock<INavigationService>();
        var editGuard = new Mock<IEditSessionGuard>();
        editGuard
            .Setup(service => service.CanLeaveCurrentSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var imageFile = new ImageFile("capture.png");

        await new OpenAboutPageUseCase(navigation.Object, editGuard.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenAboutPageRequest(), TestContext.CancellationToken);
        await new ShowHomePageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new ShowHomePageRequest(), TestContext.CancellationToken);
        await new OpenStorePageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenStorePageRequest(), TestContext.CancellationToken);
        await new OpenSettingsPageUseCase(navigation.Object, editGuard.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenSettingsPageRequest(), TestContext.CancellationToken);
        await new OpenAudioCapturePageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenAudioCapturePageRequest(), TestContext.CancellationToken);
        await new OpenImageEditPageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new OpenImageEditPageRequest(imageFile), TestContext.CancellationToken);

        navigation.Verify(service => service.Navigate(NavigationRoute.About, null, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.Home, null, true), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.Store, null, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.Settings, null, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.AudioCapture, null, false), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.ImageEdit, imageFile, false), Times.Once);
    }

    [TestMethod]
    public async Task LeavePageUseCases_WhenBackFails_NavigateHomeAndClearHistory()
    {
        var navigation = new Mock<INavigationService>();
        navigation.Setup(service => service.TryGoBack()).Returns(false);

        await new LeaveAboutPageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new LeaveAboutPageRequest(), TestContext.CancellationToken);
        await new LeaveStorePageUseCase(navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new LeaveStorePageRequest(), TestContext.CancellationToken);

        navigation.Verify(service => service.Navigate(NavigationRoute.Home, null, true), Times.Exactly(2));
    }

    [TestMethod]
    public async Task ShutdownUseCases_RespectShutdownStateAndInvokeHandler()
    {
        var shutdown = new Mock<IShutdownHandler>();
        var exit = new ExitApplicationUseCase(shutdown.Object, TestUseCaseExecutor.Instance);
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
    public async Task AudioCaptureUseCases_InvokeAudioCaptureHandler()
    {
        var audioCapture = new Mock<IAudioCaptureHandler>();
        var navigation = new Mock<INavigationService>();
        var audioFile = Mock.Of<IAudioFile>();
        audioCapture.Setup(handler => handler.StopCapture()).Returns(audioFile);

        await new StartAudioCaptureUseCase(audioCapture.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new StartAudioCaptureRequest(), TestContext.CancellationToken);
        await new PauseAudioCaptureUseCase(audioCapture.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new PauseAudioCaptureRequest(), TestContext.CancellationToken);
        await new StopAudioCaptureUseCase(audioCapture.Object, navigation.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new StopAudioCaptureRequest(), TestContext.CancellationToken);
        await new ToggleLocalAudioCaptureUseCase(audioCapture.Object, TestUseCaseExecutor.Instance)
            .ExecuteAsync(new ToggleLocalAudioCaptureRequest(), TestContext.CancellationToken);

        audioCapture.Verify(handler => handler.StartCapture(), Times.Once);
        audioCapture.Verify(handler => handler.PauseCapture(), Times.Once);
        audioCapture.Verify(handler => handler.StopCapture(), Times.Once);
        audioCapture.Verify(handler => handler.ToggleLocalAudio(), Times.Once);
        navigation.Verify(service => service.Navigate(NavigationRoute.AudioEdit, audioFile, false), Times.Once);
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
        var detector = new Mock<IFileTypeDetector>();
        var audioEdit = new Mock<IOpenAudioEditPageUseCase>();
        var imageEdit = new Mock<IOpenImageEditPageUseCase>();
        var videoEdit = new Mock<IOpenVideoEditPageUseCase>();
        detector.Setup(service => service.DetectFileType(audioPath)).Returns(CaptureFileType.Audio);
        detector.Setup(service => service.DetectFileType(imagePath)).Returns(CaptureFileType.Image);
        detector.Setup(service => service.DetectFileType(videoPath)).Returns(CaptureFileType.Video);
        audioEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenAudioEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenAudioEditPageResponse>.Success(new OpenAudioEditPageResponse()));
        imageEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenImageEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenImageEditPageResponse>.Success(new OpenImageEditPageResponse()));
        videoEdit
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenVideoEditPageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<OpenVideoEditPageResponse>.Success(new OpenVideoEditPageResponse()));
        var useCase = new OpenRecentCaptureUseCase(
            detector.Object,
            audioEdit.Object,
            imageEdit.Object,
            videoEdit.Object,
            TestUseCaseExecutor.Instance);

        Assert.IsTrue(useCase.CanExecute(new OpenRecentCaptureRequest(audioPath)));
        OpenRecentCaptureResponse audioResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(audioPath), TestContext.CancellationToken)).Value!;
        OpenRecentCaptureResponse imageResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(imagePath), TestContext.CancellationToken)).Value!;
        OpenRecentCaptureResponse videoResponse = (await useCase.ExecuteAsync(new OpenRecentCaptureRequest(videoPath), TestContext.CancellationToken)).Value!;

        Assert.IsTrue(audioResponse.Opened);
        Assert.IsTrue(imageResponse.Opened);
        Assert.IsTrue(videoResponse.Opened);
        audioEdit.Verify(useCase => useCase.ExecuteAsync(It.Is<OpenAudioEditPageRequest>(request => request.AudioFile.FilePath == audioPath), TestContext.CancellationToken), Times.Once);
        imageEdit.Verify(useCase => useCase.ExecuteAsync(It.Is<OpenImageEditPageRequest>(request => request.ImageFile.FilePath == imagePath), TestContext.CancellationToken), Times.Once);
        videoEdit.Verify(useCase => useCase.ExecuteAsync(It.Is<OpenVideoEditPageRequest>(request => request.VideoFile.FilePath == videoPath), TestContext.CancellationToken), Times.Once);
    }

    [TestMethod]
    public async Task OpenRecentCaptureUseCase_ReturnsNotOpenedForMissingOrUnknownFiles()
    {
        string unknownPath = await CreateTempFileAsync("capture.bin");
        var detector = new Mock<IFileTypeDetector>();
        detector.Setup(service => service.DetectFileType(unknownPath)).Returns(CaptureFileType.Unknown);
        var useCase = new OpenRecentCaptureUseCase(
            detector.Object,
            Mock.Of<IOpenAudioEditPageUseCase>(),
            Mock.Of<IOpenImageEditPageUseCase>(),
            Mock.Of<IOpenVideoEditPageUseCase>(),
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

    public TestContext TestContext { get; set; } = null!;
}
