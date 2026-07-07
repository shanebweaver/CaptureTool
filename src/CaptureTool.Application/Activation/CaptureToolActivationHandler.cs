using CaptureTool.Application.Abstractions.Activation;
using CaptureTool.Application.Abstractions.Capture.Overlay.OpenSelectionOverlay;
using CaptureTool.Application.Abstractions.Shell.Home.ShowHomePage;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Domain.Capture;
using System.Collections.Specialized;
using System.Web;

namespace CaptureTool.Application.Activation;

internal sealed class CaptureToolActivationHandler : IActivationHandler
{
    private readonly IOpenSelectionOverlayUseCase _openSelectionOverlay;
    private readonly IShowHomePageUseCase _showHomePage;
    private readonly ILogService _logService;
    private readonly IApplicationStartupInitializer _startupInitializer;

    private readonly SemaphoreSlim _semaphoreActivation = new(1, 1);

    public CaptureToolActivationHandler(
        IOpenSelectionOverlayUseCase openSelectionOverlay,
        IShowHomePageUseCase showHomePage,
        ILogService logService,
        IApplicationStartupInitializer startupInitializer)
    {
        _openSelectionOverlay = openSelectionOverlay;
        _showHomePage = showHomePage;
        _logService = logService;
        _startupInitializer = startupInitializer;
    }

    public async Task HandleLaunchActivationAsync()
    {
        await _semaphoreActivation.WaitAsync();

        try
        {
            await _startupInitializer.InitializeAsync();
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

            await _startupInitializer.InitializeAsync();

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

    private async Task OpenSelectionOverlayAsync(CaptureOptions captureOptions)
    {
        await _openSelectionOverlay.ExecuteAsync(new OpenSelectionOverlayRequest(captureOptions));
    }
}
