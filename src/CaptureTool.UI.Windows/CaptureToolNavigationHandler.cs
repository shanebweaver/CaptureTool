using CaptureTool.Core.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.AppController;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.UI.Windows.Xaml.Windows;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolNavigationHandler : INavigationHandler, IWindowHandleProvider
{
    private enum UXHost
    {
        None,
        MainWindow,
        SelectionOverlay,
        CaptureOverlay
    }

    private readonly IAppController _appController;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private readonly SelectionOverlayHost _selectionOverlayHost = new();
    private readonly CaptureOverlayHost _captureOverlayHost = new();
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public CaptureToolNavigationHandler(
        IAppController appController,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _appController = appController;
        _appNavigation = appNavigation;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public async void HandleNavigationRequest(INavigationRequest request)
    {
        await _semaphoreNavigation.WaitAsync();

        try
        {
            if (CaptureToolNavigationRouteHelper.IsMainWindowRoute(request.Route))
            {
                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost.LostFocus -= OnSelectionOverlayHostLostFocus;
                        _mainWindowHost.ExcludeWindowFromCapture(false);
                        _selectionOverlayHost.Close();
                        break;

                    case UXHost.CaptureOverlay:
                        _videoCaptureHandler.CancelVideoCapture();
                        _mainWindowHost.ExcludeWindowFromCapture(false);
                        _captureOverlayHost.Close();
                        break;
                }

                _mainWindowHost.Show();
                _mainWindowHost.HandleNavigationRequest(request);
                _activeHost = UXHost.MainWindow;
            }
            else if (request.Route is CaptureToolNavigationRoute imageRoute && imageRoute == CaptureToolNavigationRoute.ImageCapture)
            {
                if (request.Parameter is not CaptureOptions options)
                {
                    throw new InvalidOperationException("Image capture cannot be started without options.");
                }

                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        _mainWindowHost.ExcludeWindowFromCapture(true);
                        _mainWindowHost.Hide();
                        await Task.Delay(200);
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost.UpdateOptions(options);
                        return;

                    case UXHost.CaptureOverlay:
                        _videoCaptureHandler.CancelVideoCapture();
                        _captureOverlayHost.Close();
                        break;
                }

                _selectionOverlayHost.LostFocus += OnSelectionOverlayHostLostFocus;
                _selectionOverlayHost.Initialize(options);
                _selectionOverlayHost.Activate();
                _activeHost = UXHost.SelectionOverlay;
            }
            else if (request.Route is CaptureToolNavigationRoute videoRoute && videoRoute == CaptureToolNavigationRoute.VideoCapture)
            {
                if (request.Parameter is not NewCaptureArgs args)
                {
                    throw new InvalidOperationException("Video capture cannot be started without arguments.");
                }

                switch (_activeHost)
                {
                    case UXHost.MainWindow:
                        _mainWindowHost.ExcludeWindowFromCapture(true);
                        _mainWindowHost.Hide();
                        await Task.Delay(200);
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost.LostFocus -= OnSelectionOverlayHostLostFocus;
                        _selectionOverlayHost.Close();
                        break;

                    case UXHost.CaptureOverlay:
                        return;
                }

                _captureOverlayHost.Initialize(args);
                _captureOverlayHost.Activate();
                _activeHost = UXHost.CaptureOverlay;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(request), $"No handler found for route: {request.Route}");
            }
        }
        finally
        {
            _semaphoreNavigation.Release();
        }
    }

    private void OnSelectionOverlayHostLostFocus(object? sender, EventArgs e)
    {
        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            _appController.Shutdown();
        }
    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost.Handle;
    }
}
