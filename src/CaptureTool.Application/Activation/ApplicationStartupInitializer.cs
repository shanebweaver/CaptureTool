using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;

namespace CaptureTool.Application.Activation;

internal sealed class ApplicationStartupInitializer : IApplicationStartupInitializer
{
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationHandler _navigationHandler;
    private readonly INavigationService _navigationService;
    private readonly IStorageService _storageService;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private bool _isInitialized;

    public ApplicationStartupInitializer(
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        ILocalizationService localizationService,
        INavigationHandler navigationHandler,
        INavigationService navigationService,
        IStorageService storageService)
    {
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _logService = logService;
        _localizationService = localizationService;
        _navigationHandler = navigationHandler;
        _navigationService = navigationService;
        _storageService = storageService;
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

            bool isLoggingEnabled = _settingsService.Get(CaptureToolSettings.VerboseLogging);
            if (isLoggingEnabled)
            {
                _logService.Enable();
            }

            string languageOverride = _settingsService.Get(CaptureToolSettings.Settings_LanguageOverride);
            _localizationService.Initialize(languageOverride);

            _navigationService.SetNavigationHandler(_navigationHandler);

            _isInitialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = _storageService.GetApplicationDataFolderPath();
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
