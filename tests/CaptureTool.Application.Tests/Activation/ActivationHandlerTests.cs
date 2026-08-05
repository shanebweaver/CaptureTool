using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.EditSessions;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Activation;
using CaptureTool.Application.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.EditSessions;
using CaptureTool.Application.Navigation;
using CaptureTool.Application.Shell.Home.ShowHomePage;
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
        fixture.Settings.Verify(service => service.Get(CaptureToolSettings.Settings_TelemetryConsent), Times.Once);
        fixture.TelemetryConsent.Verify(
            service => service.SetState(TelemetryConsentState.Unknown),
            Times.Once);
        fixture.AppMetrics.Verify(
            service => service.InitializeAsync(
                Path.Combine(fixture.AppDataFolder, "Metrics.json"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.AppMetrics.Verify(service => service.RecordAppLaunchAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.LogService.Verify(service => service.Enable(), Times.Once);
        fixture.Localization.Verify(service => service.Initialize("fr-FR"), Times.Once);
        fixture.NavigationService.Verify(service => service.SetNavigationHandler(fixture.NavigationHandler.Object), Times.Once);
        fixture.ScratchArtifactStore.Verify(
            service => service.ScavengeStaleArtifacts(TimeSpan.FromDays(7)),
            Times.Once);
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
            service => service.NavigateAsync(
                target.Route,
                target.Parameter,
                target.ClearHistory,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task ApplicationStartupInitializer_ShouldRestoreGrantedTelemetryConsent()
    {
        StartupInitializerFixture fixture = new(
            telemetryConsentValue: TelemetryConsentSettingValues.Granted);

        await fixture.Initializer.InitializeAsync(TestContext.CancellationToken);

        fixture.TelemetryConsent.Verify(
            service => service.SetState(TelemetryConsentState.Granted),
            Times.Once);
    }

    [TestMethod]
    public async Task ApplicationStartupInitializer_WhenScratchScavengingFails_ShouldContinueInitialization()
    {
        StartupInitializerFixture fixture = new();
        var exception = new IOException("scratch unavailable");
        fixture.ScratchArtifactStore
            .Setup(service => service.ScavengeStaleArtifacts(TimeSpan.FromDays(7)))
            .Throws(exception);

        await fixture.Initializer.InitializeAsync(TestContext.CancellationToken);

        fixture.NavigationService.Verify(
            service => service.SetNavigationHandler(fixture.NavigationHandler.Object),
            Times.Once);
        fixture.LogService.Verify(
            service => service.LogException(exception, "Failed to scavenge stale scratch artifacts."),
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

    [TestMethod]
    [DataRow(EditSessionLeaveDecision.SaveToSource, true)]
    [DataRow(EditSessionLeaveDecision.Discard, true)]
    [DataRow(EditSessionLeaveDecision.Cancel, false)]
    public async Task HandleProtocolActivationAsync_WithDirtyImageSession_ObeysLeaveDecision(
        EditSessionLeaveDecision decision,
        bool shouldNavigate)
    {
        var session = new Mock<ISourceSaveableSession>();
        session.SetupGet(value => value.HasUnsavedChanges).Returns(true);
        session
            .Setup(value => value.SaveToSourceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<INavigationService> navigation = await HandleProtocolWithDirtySessionAsync(
            session.Object,
            decision,
            new Uri("ms-screenclip://capture?source=PrintScreen"));

        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.SelectionOverlay,
                It.Is<CaptureOptions>(options => options.CaptureMode == CaptureMode.Image),
                false,
                It.IsAny<CancellationToken>()),
            shouldNavigate ? Times.Once : Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.Home,
                null,
                true,
                It.IsAny<CancellationToken>()),
            shouldNavigate ? Times.Once : Times.Never);
        session.Verify(
            value => value.SaveToSourceAsync(It.IsAny<CancellationToken>()),
            decision == EditSessionLeaveDecision.SaveToSource ? Times.Once : Times.Never);
    }

    [TestMethod]
    [DataRow(EditSessionLeaveDecision.SaveAs, true)]
    [DataRow(EditSessionLeaveDecision.Discard, true)]
    [DataRow(EditSessionLeaveDecision.Cancel, false)]
    public async Task HandleProtocolActivationAsync_WithDirtyVideoSession_ObeysLeaveDecision(
        EditSessionLeaveDecision decision,
        bool shouldNavigate)
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(value => value.HasUnsavedChanges).Returns(true);
        session
            .Setup(value => value.SaveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Mock<INavigationService> navigation = await HandleProtocolWithDirtySessionAsync(
            session.Object,
            decision,
            new Uri("ms-screenclip://capture?source=ScreenRecorderHotKey"));

        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.SelectionOverlay,
                It.Is<CaptureOptions>(options => options.CaptureMode == CaptureMode.Video),
                false,
                It.IsAny<CancellationToken>()),
            shouldNavigate ? Times.Once : Times.Never);
        navigation.Verify(
            service => service.NavigateAsync(
                NavigationRoute.Home,
                null,
                true,
                It.IsAny<CancellationToken>()),
            shouldNavigate ? Times.Once : Times.Never);
        session.Verify(
            value => value.SaveAsync(It.IsAny<CancellationToken>()),
            decision == EditSessionLeaveDecision.SaveAs ? Times.Once : Times.Never);
    }

    [TestMethod]
    public async Task HandleProtocolActivationAsync_WithUnknownSourceAndCanceledLeave_DoesNotShowHome()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(value => value.HasUnsavedChanges).Returns(true);

        Mock<INavigationService> navigation = await HandleProtocolWithDirtySessionAsync(
            session.Object,
            EditSessionLeaveDecision.Cancel,
            new Uri("ms-screenclip://capture?source=Other"));

        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task HandleLaunchActivationAsync_WithDirtySessionAndCanceledLeave_DoesNotNavigateToLaunchTarget()
    {
        var session = new Mock<IEditableSession>();
        session.SetupGet(value => value.HasUnsavedChanges).Returns(true);
        LaunchNavigationTarget launchTarget = new(NavigationRoute.ImageEdit, "image.png");
        (CaptureToolActivationHandler handler, Mock<INavigationService> navigation) =
            CreateHandlerWithDirtySession(
                session.Object,
                EditSessionLeaveDecision.Cancel,
                launchTarget);

        await handler.HandleLaunchActivationAsync();

        navigation.Verify(
            service => service.NavigateAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task<Mock<INavigationService>> HandleProtocolWithDirtySessionAsync(
        IEditableSession session,
        EditSessionLeaveDecision decision,
        Uri protocolUri)
    {
        (CaptureToolActivationHandler handler, Mock<INavigationService> navigation) =
            CreateHandlerWithDirtySession(session, decision);

        await handler.HandleProtocolActivationAsync(protocolUri);
        return navigation;
    }

    private static (CaptureToolActivationHandler Handler, Mock<INavigationService> Navigation)
        CreateHandlerWithDirtySession(
            IEditableSession session,
            EditSessionLeaveDecision decision,
            LaunchNavigationTarget? launchTarget = null)
    {
        var activeSession = new ActiveEditSessionService();
        activeSession.SetCurrentSession(session);
        var confirmation = new Mock<IEditSessionConfirmationService>();
        confirmation
            .Setup(service => service.ConfirmLeaveAsync(session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard))
            .Returns(true);
        var editGuard = new EditSessionGuard(activeSession, confirmation.Object, settings.Object);
        var audioGuard = new Mock<CaptureTool.Application.Abstractions.Capture.Audio.IAudioCaptureNavigationGuard>();
        audioGuard
            .Setup(guard => guard.CanNavigateAwayFromActiveCaptureAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var navigation = new Mock<INavigationService>();
        TestNavigationService.AcceptAll(navigation);
        var coordinator = new NavigationCoordinator(navigation.Object, editGuard, audioGuard.Object);
        var openSelection = new OpenSelectionOverlayUseCase(coordinator, activeSession, TestUseCaseExecutor.Instance);
        var showHome = new ShowHomePageUseCase(coordinator, TestUseCaseExecutor.Instance);
        var launchTargetProvider = new Mock<ILaunchNavigationTargetProvider>();
        launchTargetProvider
            .Setup(provider => provider.GetLaunchNavigationTarget())
            .Returns(launchTarget);
        var handler = new CaptureToolActivationHandler(
            openSelection,
            showHome,
            Mock.Of<ILogService>(),
            new FakeStartupInitializer(),
            launchTargetProvider.Object,
            coordinator);

        return (handler, navigation);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed class ActivationHandlerFixture
    {
        public ActivationHandlerFixture()
        {
            TestNavigationService.AcceptAll(NavigationService);
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
                TestNavigationCoordinator.Create(NavigationService.Object));
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
        public StartupInitializerFixture(
            bool verboseLogging = false,
            string languageOverride = "",
            string telemetryConsentValue = TelemetryConsentSettingValues.Unknown)
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
            Settings
                .Setup(service => service.Get(CaptureToolSettings.Settings_TelemetryConsent))
                .Returns(telemetryConsentValue);
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
                StorageService.Object,
                ScratchArtifactStore.Object,
                telemetryConsentService: TelemetryConsent.Object);
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
        public Mock<IScratchArtifactStore> ScratchArtifactStore { get; } = new();
        public Mock<ITelemetryConsentService> TelemetryConsent { get; } = new();
    }
}
