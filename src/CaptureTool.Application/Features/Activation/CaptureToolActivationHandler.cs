using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.CaptureOverlay.OpenSelectionOverlay;
using CaptureTool.Application.Features.Home.ShowHomePage;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Activation;
using CaptureTool.Infrastructure.Abstractions.Cancellation;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Settings;
using System.Collections.Specialized;
using System.Web;

namespace CaptureTool.Application.Features.Activation;

public sealed class CaptureToolActivationHandler : IActivationHandler
{
    private readonly IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse> _openSelectionOverlay;
    private readonly IUseCase<ShowHomePageRequest, ShowHomePageResponse> _showHomePage;
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
        IUseCase<OpenSelectionOverlayRequest, OpenSelectionOverlayResponse> openSelectionOverlay,
        IUseCase<ShowHomePageRequest, ShowHomePageResponse> showHomePage,
        ICancellationService cancellationService,
        ISettingsService settingsService,
        ILogService logService,
        ILocalizationService localizationService,
        INavigationHandler navigationHandler,
        INavigationService navigationService)
    {
        _openSelectionOverlay = openSelectionOverlay;
        _showHomePage = showHomePage;
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
            await _showHomePage.ExecuteAsync(new ShowHomePageRequest());
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
                await OpenSelectionOverlayAsync(CaptureOptions.ImageDefault);
            }
            else if (source.Equals("ScreenRecorderHotKey", StringComparison.InvariantCultureIgnoreCase) || isRecordingType)
            {
                await OpenSelectionOverlayAsync(CaptureOptions.VideoDefault);
            }
            else if (source.Equals("HotKey", StringComparison.InvariantCultureIgnoreCase))
            {
                await OpenSelectionOverlayAsync(CaptureOptions.ImageDefault);
            }
            else
            {
                await _showHomePage.ExecuteAsync(new ShowHomePageRequest());
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

    private Task OpenSelectionOverlayAsync(CaptureOptions captureOptions)
    {
        return _openSelectionOverlay.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions));
    }

    private async Task InitializeSettingsServiceAsync(CancellationToken cancellationToken)
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string settingsFilePath = Path.Combine(appDataPath, "Settings.json");
        await _settingsService.InitializeAsync(settingsFilePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}