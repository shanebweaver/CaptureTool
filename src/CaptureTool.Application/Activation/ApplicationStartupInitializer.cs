using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Capture.Assets;
using CaptureTool.Application.Analysis.Intake;
using CaptureTool.Application.Analysis.Processing;
using CaptureTool.Application.Abstractions.Analysis.Memory;

namespace CaptureTool.Application.Activation;

internal sealed class ApplicationStartupInitializer : IApplicationStartupInitializer
{
    private static readonly TimeSpan ScratchArtifactMaximumAge = TimeSpan.FromDays(7);

    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly IAppMetricsService _appMetricsService;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationHandler _navigationHandler;
    private readonly INavigationService _navigationService;
    private readonly IStorageService _storageService;
    private readonly IScratchArtifactStore _scratchArtifactStore;
    private readonly ICaptureAssetBootstrapper _captureAssetBootstrapper;
    private readonly ITelemetryService? _telemetryService;
    private readonly ITelemetryConsentService? _telemetryConsentService;
    private readonly CaptureAnalysisWorkerHost? _captureAnalysisWorkerHost;
    private readonly ICaptureAnalysisReconciler? _captureAnalysisReconciler;
    private readonly ICaptureMemoryWorkflow? _captureMemoryWorkflow;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private bool _isInitialized;

    public ApplicationStartupInitializer(
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        IAppMetricsService appMetricsService,
        ILocalizationService localizationService,
        INavigationHandler navigationHandler,
        INavigationService navigationService,
        IStorageService storageService,
        IScratchArtifactStore scratchArtifactStore,
        ICaptureAssetBootstrapper captureAssetBootstrapper,
        ITelemetryService? telemetryService = null,
        ITelemetryConsentService? telemetryConsentService = null,
        CaptureAnalysisWorkerHost? captureAnalysisWorkerHost = null,
        ICaptureAnalysisReconciler? captureAnalysisReconciler = null,
        ICaptureMemoryWorkflow? captureMemoryWorkflow = null)
    {
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _logService = logService;
        _appMetricsService = appMetricsService;
        _localizationService = localizationService;
        _navigationHandler = navigationHandler;
        _navigationService = navigationService;
        _storageService = storageService;
        _scratchArtifactStore = scratchArtifactStore;
        _captureAssetBootstrapper = captureAssetBootstrapper;
        _telemetryService = telemetryService;
        _telemetryConsentService = telemetryConsentService;
        _captureAnalysisWorkerHost = captureAnalysisWorkerHost;
        _captureAnalysisReconciler = captureAnalysisReconciler;
        _captureMemoryWorkflow = captureMemoryWorkflow;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            if (_isInitialized)
            {
                return;
            }

            using CancellationTokenSource cancellationTokenSource =
                _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            await InitializeSettingsServiceAsync(cancellationTokenSource.Token);
            InitializeTelemetryConsent();
            await InitializeMetricsServiceAsync(cancellationTokenSource.Token);

            bool isLoggingEnabled = _settingsService.Get(CaptureToolSettings.VerboseLogging);
            if (isLoggingEnabled)
            {
                _logService.Enable();
            }

            ScavengeScratchArtifacts();
            await InitializeCaptureAssetsAsync(cancellationTokenSource.Token);
            await ReconcileCaptureAnalysisAsync(cancellationTokenSource.Token);
            _captureAnalysisWorkerHost?.Start();
            if (_captureMemoryWorkflow != null)
            {
                try { await _captureMemoryWorkflow.ResumeAsync(cancellationTokenSource.Token); }
                catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested) { throw; }
                catch (Exception ex) { _logService.LogException(ex, "Failed to resume Capture Memory work."); }
            }

            string languageOverride = _settingsService.Get(CaptureToolSettings.Settings_LanguageOverride);
            _localizationService.Initialize(languageOverride);

            _navigationService.SetNavigationHandler(_navigationHandler);

            _isInitialized = true;
            _telemetryService?.TrackEvent(TelemetryEvents.AppStarted);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ScavengeScratchArtifacts()
    {
        try
        {
            _scratchArtifactStore.ScavengeStaleArtifacts(ScratchArtifactMaximumAge);
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to scavenge stale scratch artifacts.");
        }
    }

    private async Task InitializeCaptureAssetsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _captureAssetBootstrapper.InitializeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to initialize capture assets.");
        }
    }

    private async Task ReconcileCaptureAnalysisAsync(CancellationToken cancellationToken)
    {
        if (_captureAnalysisReconciler == null)
        {
            return;
        }

        try
        {
            await _captureAnalysisReconciler.ReconcileStartupAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to reconcile Capture Analysis intake.");
        }
    }

    private void InitializeTelemetryConsent()
    {
        string settingValue = _settingsService.Get(CaptureToolSettings.Settings_TelemetryConsent);
        _telemetryConsentService?.SetState(TelemetryConsentSettingValues.Parse(settingValue));
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = _storageService.GetApplicationDataFolderPath();
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task InitializeMetricsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = _storageService.GetApplicationDataFolderPath();
        string metricsFilePath = Path.Combine(appDataPath, "Metrics.json");
        await _appMetricsService.InitializeAsync(metricsFilePath, cancellationToken);
        await _appMetricsService.RecordAppLaunchAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
