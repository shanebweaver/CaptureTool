using CaptureTool.Application.Implementations.Settings;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Domain.Capture.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Activation;
using CaptureTool.Infrastructure.Interfaces.Cancellation;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Logging;
using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Infrastructure.Interfaces.Settings;
using System.Collections.Specialized;
using System.Web;

namespace CaptureTool.Application.Implementations.Services.Activation;

public sealed partial class CaptureToolActivationHandler : IActivationHandler
{
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppNavigation _appNavigation;
    private readonly INavigationHandler _navigationHandler;
    private readonly INavigationService _navigationService;
    private readonly IFeatureManager _featureManager;

    private readonly SemaphoreSlim _semaphoreActivation = new(1, 1);
    private readonly SemaphoreSlim _semaphoreInit = new(1, 1);

    private bool _isInitialized;

    public CaptureToolActivationHandler(
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        ILocalizationService localizationService,
        IAppNavigation appNavigation,
        INavigationHandler navigationHandler,
        INavigationService navigationService,
        IFeatureManager featureManager)
    {
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _logService = logService;
        _localizationService = localizationService;
        _appNavigation = appNavigation;
        _navigationHandler = navigationHandler;
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

            string languageOverride = _settingsService.Get(CaptureToolSettings.Settings_LanguageOverride);
            _localizationService.Initialize(languageOverride);

            _navigationService.SetNavigationHandler(_navigationHandler);

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
