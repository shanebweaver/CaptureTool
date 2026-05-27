using CaptureTool.Application.UseCases.Home;
using CaptureTool.Application.UseCases.Settings;
using CaptureTool.Application.UseCases.CaptureOverlay;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Activation;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Settings;
using System.Collections.Specialized;
using System.Web;

namespace CaptureTool.Application.UseCases.Activation;

public sealed partial class CaptureToolActivationHandler : IActivationHandler
{
    private readonly OpenSelectionOverlayAppCommand _openImageCaptureOverlayAppCommand;
    private readonly ShowHomePageAppCommand _showHomePageAppCommand;
    private readonly ICancellationService _cancellationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localizationService;
    private readonly INavigationHandler _navigationHandler;
    private readonly INavigationService _navigationService;

    private readonly SemaphoreSlim _semaphoreActivation = new(1, 1);
    private readonly SemaphoreSlim _semaphoreInit = new(1, 1);

    private bool _isInitialized;

    public CaptureToolActivationHandler(
        OpenSelectionOverlayAppCommand openImageCaptureOverlayAppCommand,
        ShowHomePageAppCommand showHomePageAppCommand,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        ILocalizationService localizationService,
        INavigationHandler navigationHandler,
        INavigationService navigationService)
    {
        _openImageCaptureOverlayAppCommand = openImageCaptureOverlayAppCommand;
        _showHomePageAppCommand = showHomePageAppCommand;
        _cancellationService = cancellationService;
        _settingsService = settingsService;
        _logService = logService;
        _localizationService = localizationService;
        _navigationHandler = navigationHandler;
        _navigationService = navigationService;
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
            _showHomePageAppCommand.Execute();
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
                _openImageCaptureOverlayAppCommand.Execute(CaptureOptions.ImageDefault);
            }
            else if (source.Equals("ScreenRecorderHotKey", StringComparison.InvariantCultureIgnoreCase) || isRecordingType)
            {
                _openImageCaptureOverlayAppCommand.Execute(CaptureOptions.VideoDefault);
            }
            else if (source.Equals("HotKey", StringComparison.InvariantCultureIgnoreCase))
            {
                _openImageCaptureOverlayAppCommand.Execute(CaptureOptions.ImageDefault);
            }
            else
            {
                _showHomePageAppCommand.Execute();
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
