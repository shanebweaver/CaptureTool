using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Settings;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Interfaces.Activation;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Settings;
using System.Collections.Specialized;
using System.Web;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolActivationHandler : IActivationHandler
{
    private readonly IAppController _appController;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNavigation _appNavigation;
    private readonly INavigationService _navigationService;
    private readonly IFeatureManager _featureManager;

    private readonly SemaphoreSlim _semaphoreActivation = new(1, 1);
    private readonly SemaphoreSlim _semaphoreInit = new(1, 1);

    private bool _isInitialized;

    public CaptureToolActivationHandler(
        IAppController appController,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        ILocalizationService localizationService,
        IAppNavigation appNavigation,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _appController = appController;
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _logService = logService;
        _localizationService = localizationService;
        _appNavigation = appNavigation;
        _navigationService = navigationService;
        _featureManager = featureManager;
    }

    private async Task InitializeAsync()
    {
        await _semaphoreInit.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            CancellationTokenSource cancellationTokenSource = _cancellationService.GetLinkedCancellationTokenSource();
            await InitializeSettingsServiceAsync(cancellationTokenSource.Token);

            bool isLoggingEnabled = _settingsService.Get(CaptureToolSettings.VerboseLogging);
            if (isLoggingEnabled)
            {
                _logService.Enable();
            }

            _localizationService.Initialize(CaptureToolSettings.Settings_LanguageOverride);

            _navigationService.SetNavigationHandler(_appController);

            _isInitialized = true;
        }
        finally
        {
            _semaphoreInit.Release();
        }
    }

    public async Task HandleLaunchActivationAsync()
    {
        await _semaphoreActivation.WaitAsync();

        try
        {
            await InitializeAsync();
            _appNavigation.GoHome();
        }
        finally
        {
            _semaphoreActivation.Release();
        }
    }

    public async Task HandleProtocolActivationAsync(Uri protocolUri)
    {
        await _semaphoreActivation.WaitAsync();

        try
        {
            if (!protocolUri.Scheme.Equals("ms-screenclip", StringComparison.InvariantCultureIgnoreCase))
            {
                return;
            }

            await InitializeAsync();

            NameValueCollection queryParams = HttpUtility.ParseQueryString(protocolUri.Query) ?? [];
            bool isRecordingType = queryParams.Get("type") is string type && type.Equals("recording", StringComparison.InvariantCultureIgnoreCase);

            string source = queryParams.Get("source") ?? string.Empty;
            if (source.Equals("PrintScreen", StringComparison.InvariantCultureIgnoreCase))
            {
                _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
            }
            else if (source.Equals("ScreenRecorderHotKey", StringComparison.InvariantCultureIgnoreCase) || isRecordingType)
            {
                if (_featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture))
                {
                    _appNavigation.GoToImageCapture(CaptureOptions.VideoDefault);
                }
                else
                {
                    _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
                }
            }
            else if (source.Equals("HotKey", StringComparison.InvariantCultureIgnoreCase))
            {
                _appNavigation.GoToImageCapture(CaptureOptions.ImageDefault);
            }
            else
            {
                _appNavigation.GoHome();
            }
        }
        catch (Exception ex)
        {
            _logService.LogException(ex, "Failed to handle protocol activation.");
        }
        finally
        {
            _semaphoreActivation.Release();
        }
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
