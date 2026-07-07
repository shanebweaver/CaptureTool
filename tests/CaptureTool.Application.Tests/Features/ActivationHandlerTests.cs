using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Features.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Activation;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Features;

[TestClass]
public sealed class ActivationHandlerTests
{
    [TestMethod]
    public async Task HandleLaunchActivationAsync_ShouldInitializeOnceAndShowHome()
    {
        ActivationHandlerFixture fixture = new(verboseLogging: true, languageOverride: "fr-FR");

        await fixture.Handler.HandleLaunchActivationAsync();
        await fixture.Handler.HandleLaunchActivationAsync();

        fixture.Settings.Verify(
            service => service.InitializeAsync(
                Path.Combine(fixture.AppDataFolder, "Settings.json"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Settings.Verify(service => service.Get(CaptureToolSettings.VerboseLogging), Times.Once);
        fixture.Settings.Verify(service => service.Get(CaptureToolSettings.Settings_LanguageOverride), Times.Once);
        fixture.LogService.Verify(service => service.Enable(), Times.Once);
        fixture.Localization.Verify(service => service.Initialize("fr-FR"), Times.Once);
        fixture.NavigationService.Verify(service => service.SetNavigationHandler(fixture.NavigationHandler.Object), Times.Once);
        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldIgnoreNonScreenClipSchemes()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("https://example.com/"));

        fixture.Settings.Verify(
            service => service.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.OpenSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldOpenImageSelectionForImageSources()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?source=PrintScreen"));
        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?source=HotKey"));

        fixture.OpenSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<OpenSelectionOverlayRequest>(request => request.CaptureOptions.CaptureMode == CaptureMode.Image),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldOpenVideoSelectionForRecorderSources()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?source=ScreenRecorderHotKey"));
        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?type=recording"));

        fixture.OpenSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(
                It.Is<OpenSelectionOverlayRequest>(request => request.CaptureOptions.CaptureMode == CaptureMode.Video),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldShowHomeForUnknownSources()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?source=Other"));

        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.OpenSelectionOverlay.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldLogExceptions()
    {
        ActivationHandlerFixture fixture = new();
        var exception = new InvalidOperationException("activation failed");
        fixture.OpenSelectionOverlay
            .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("ms-screenclip://capture?source=PrintScreen"));

        fixture.LogService.Verify(
            service => service.LogException(exception, "Failed to handle protocol activation."),
            Times.Once);
    }

    private sealed class ActivationHandlerFixture
    {
        public ActivationHandlerFixture(bool verboseLogging = false, string languageOverride = "")
        {
            CancellationService
                .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken?>()))
                .Returns(() => new CancellationTokenSource());
            Settings
                .Setup(service => service.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Settings
                .Setup(service => service.Get(CaptureToolSettings.VerboseLogging))
                .Returns(verboseLogging);
            Settings
                .Setup(service => service.Get(CaptureToolSettings.Settings_LanguageOverride))
                .Returns(languageOverride);
            OpenSelectionOverlay
                .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(UseCaseResponse<OpenSelectionOverlayResponse>.Success(new OpenSelectionOverlayResponse()));
            ShowHomePage
                .Setup(useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(UseCaseResponse<ShowHomePageResponse>.Success(new ShowHomePageResponse()));
            StorageService
                .Setup(service => service.GetApplicationDataFolderPath())
                .Returns(AppDataFolder);

            Handler = new CaptureToolActivationHandler(
                OpenSelectionOverlay.Object,
                ShowHomePage.Object,
                CancellationService.Object,
                Settings.Object,
                LogService.Object,
                Localization.Object,
                NavigationHandler.Object,
                NavigationService.Object,
                StorageService.Object);
        }

        public CaptureToolActivationHandler Handler { get; }
        public string AppDataFolder { get; } = @"C:\CaptureTool\AppData";
        public Mock<IOpenSelectionOverlayUseCase> OpenSelectionOverlay { get; } = new();
        public Mock<IShowHomePageUseCase> ShowHomePage { get; } = new();
        public Mock<ICancellationService> CancellationService { get; } = new();
        public Mock<ISettingsService> Settings { get; } = new();
        public Mock<ILogService> LogService { get; } = new();
        public Mock<ILocalizationService> Localization { get; } = new();
        public Mock<INavigationHandler> NavigationHandler { get; } = new();
        public Mock<INavigationService> NavigationService { get; } = new();
        public Mock<IStorageService> StorageService { get; } = new();
    }
}
