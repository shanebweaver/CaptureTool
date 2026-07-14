using CaptureTool.Application.Tests;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Activation;
using CaptureTool.Domain.Capture;
using Moq;

namespace CaptureTool.Application.Tests.Activation;

[TestClass]
public sealed class ActivationHandlerTests
{
    [TestMethod]
    public async Task ApplicationStartupInitializer_ShouldInitializeOnce()
    {
        StartupInitializerFixture fixture = new(verboseLogging: true, languageOverride: "fr-FR");

        await fixture.Initializer.InitializeAsync(TestContext.CancellationToken);
        await fixture.Initializer.InitializeAsync(TestContext.CancellationToken);

        fixture.Settings.Verify(
            service => service.InitializeAsync(
                Path.Combine(fixture.AppDataFolder, "Settings.json"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Settings.Verify(service => service.Get(CaptureToolSettings.VerboseLogging), Times.Once);
        fixture.Settings.Verify(service => service.Get(CaptureToolSettings.Settings_LanguageOverride), Times.Once);
        fixture.AppMetrics.Verify(
            service => service.InitializeAsync(
                Path.Combine(fixture.AppDataFolder, "Metrics.json"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.AppMetrics.Verify(service => service.RecordAppLaunchAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.LogService.Verify(service => service.Enable(), Times.Once);
        fixture.Localization.Verify(service => service.Initialize("fr-FR"), Times.Once);
        fixture.NavigationService.Verify(service => service.SetNavigationHandler(fixture.NavigationHandler.Object), Times.Once);
    }

    [TestMethod]
    public async Task HandleLaunchActivationAsync_ShouldInitializeAndShowHome()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleLaunchActivationAsync();
        await fixture.Handler.HandleLaunchActivationAsync();

        Assert.AreEqual(2, fixture.StartupInitializer.InitializeCallCount);
        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task HandleLaunchActivationAsync_WithLaunchTarget_ShouldNavigateToLaunchTarget()
    {
        ActivationHandlerFixture fixture = new();
        LaunchNavigationTarget target = new("ImageEdit", "image.png");
        fixture.LaunchNavigationTargetProvider
            .Setup(provider => provider.GetLaunchNavigationTarget())
            .Returns(target);

        await fixture.Handler.HandleLaunchActivationAsync();

        fixture.ShowHomePage.Verify(
            useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.NavigationService.Verify(
            service => service.Navigate(target.Route, target.Parameter, target.ClearHistory),
            Times.Once);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_ShouldIgnoreNonScreenClipSchemes()
    {
        ActivationHandlerFixture fixture = new();

        await fixture.Handler.HandleProtocolActivationAsync(new Uri("https://example.com/"));

        Assert.AreEqual(0, fixture.StartupInitializer.InitializeCallCount);
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

    public TestContext TestContext { get; set; } = null!;

    private sealed class ActivationHandlerFixture
    {
        public ActivationHandlerFixture()
        {
            OpenSelectionOverlay
                .Setup(useCase => useCase.ExecuteAsync(It.IsAny<OpenSelectionOverlayRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(UseCaseResponse<OpenSelectionOverlayResponse>.Success(new OpenSelectionOverlayResponse()));
            ShowHomePage
                .Setup(useCase => useCase.ExecuteAsync(It.IsAny<ShowHomePageRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(UseCaseResponse<ShowHomePageResponse>.Success(new ShowHomePageResponse()));

            Handler = new CaptureToolActivationHandler(
                OpenSelectionOverlay.Object,
                ShowHomePage.Object,
                LogService.Object,
                StartupInitializer,
                LaunchNavigationTargetProvider.Object,
                NavigationService.Object);
        }

        public CaptureToolActivationHandler Handler { get; }
        public Mock<IOpenSelectionOverlayUseCase> OpenSelectionOverlay { get; } = new();
        public Mock<IShowHomePageUseCase> ShowHomePage { get; } = new();
        public Mock<ILogService> LogService { get; } = new();
        public Mock<ILaunchNavigationTargetProvider> LaunchNavigationTargetProvider { get; } = new();
        public Mock<INavigationService> NavigationService { get; } = new();
        public FakeStartupInitializer StartupInitializer { get; } = new();
    }

    private sealed class FakeStartupInitializer : IApplicationStartupInitializer
    {
        public int InitializeCallCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StartupInitializerFixture
    {
        public StartupInitializerFixture(bool verboseLogging = false, string languageOverride = "")
        {
            CancellationService
                .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken?>()))
                .Returns(() => new CancellationTokenSource());
            Settings
                .Setup(service => service.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AppMetrics
                .Setup(service => service.InitializeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            AppMetrics
                .Setup(service => service.RecordAppLaunchAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            Settings
                .Setup(service => service.Get(CaptureToolSettings.VerboseLogging))
                .Returns(verboseLogging);
            Settings
                .Setup(service => service.Get(CaptureToolSettings.Settings_LanguageOverride))
                .Returns(languageOverride);
            StorageService
                .Setup(service => service.GetApplicationDataFolderPath())
                .Returns(AppDataFolder);

            Initializer = new ApplicationStartupInitializer(
                CancellationService.Object,
                Settings.Object,
                LogService.Object,
                AppMetrics.Object,
                Localization.Object,
                NavigationHandler.Object,
                NavigationService.Object,
                StorageService.Object);
        }

        public ApplicationStartupInitializer Initializer { get; }
        public string AppDataFolder { get; } = @"C:\CaptureTool\AppData";
        public Mock<ICancellationService> CancellationService { get; } = new();
        public Mock<ISettingsService> Settings { get; } = new();
        public Mock<IAppMetricsService> AppMetrics { get; } = new();
        public Mock<ILogService> LogService { get; } = new();
        public Mock<ILocalizationService> Localization { get; } = new();
        public Mock<INavigationHandler> NavigationHandler { get; } = new();
        public Mock<INavigationService> NavigationService { get; } = new();
        public Mock<IStorageService> StorageService { get; } = new();
    }
}
