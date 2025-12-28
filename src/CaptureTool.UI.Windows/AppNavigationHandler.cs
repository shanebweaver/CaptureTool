using CaptureTool.Core.Implementations.Services.Navigation;
using CaptureTool.Core.Interfaces.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.UI.Windows.Xaml.Windows;

namespace CaptureTool.UI.Windows;

internal partial class AppNavigationHandler : INavigationHandler, IWindowHandleProvider
{
    private enum UXHost
    {
        None,
        MainWindow,
        SelectionOverlay,
        CaptureOverlay
    }

    private readonly IShutdownHandler _shutdownHandler;
    private readonly IAppNavigation _appNavigation;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private SelectionOverlayHost? _selectionOverlayHost;
    private CaptureOverlayHost? _captureOverlayHost;
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public AppNavigationHandler(
        IShutdownHandler shutdownHandler,
        IAppNavigation appNavigation,
        IVideoCaptureHandler videoCaptureHandler)
    {
        _shutdownHandler = shutdownHandler;
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
                        await DisposeSelectionOverlayHostAsync();
                        _mainWindowHost.ExcludeWindowFromCapture(false);
                        break;

                    case UXHost.CaptureOverlay:
                        _videoCaptureHandler.CancelVideoCapture();
                        await DisposeCaptureOverlayHostAsync();
                        _mainWindowHost.ExcludeWindowFromCapture(false);
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
                        _selectionOverlayHost?.UpdateOptions(options);
                        return;

                    case UXHost.CaptureOverlay:
                        _videoCaptureHandler.CancelVideoCapture();
                        await DisposeCaptureOverlayHostAsync();
                        break;
                }

                // Create fresh instance using factory pattern
                await CreateSelectionOverlayHostAsync(options);
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
                        await DisposeSelectionOverlayHostAsync();
                        break;

                    case UXHost.CaptureOverlay:
                        return;
                }

                // Create fresh instance using factory pattern
                await CreateCaptureOverlayHostAsync(args);
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

    private async Task CreateSelectionOverlayHostAsync(CaptureOptions options)
    {
        // Dispose previous instance if it exists
        await DisposeSelectionOverlayHostAsync();

        // Create fresh instance
        _selectionOverlayHost = new SelectionOverlayHost();
        _selectionOverlayHost.LostFocus += OnSelectionOverlayHostLostFocus;
        _selectionOverlayHost.Initialize(options);
        _selectionOverlayHost.Activate();
    }

    private async Task DisposeSelectionOverlayHostAsync()
    {
        if (_selectionOverlayHost == null)
        {
            return;
        }

        try
        {
            _selectionOverlayHost.LostFocus -= OnSelectionOverlayHostLostFocus;
            _selectionOverlayHost.Close();
            _selectionOverlayHost.Dispose();
        }
        catch { }
        finally
        {
            _selectionOverlayHost = null;
        }

        // Force garbage collection to release large pixel buffers
        await Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        });
    }

    private async Task CreateCaptureOverlayHostAsync(NewCaptureArgs args)
    {
        // Dispose previous instance if it exists
        await DisposeCaptureOverlayHostAsync();

        // Create fresh instance
        _captureOverlayHost = new CaptureOverlayHost();
        _captureOverlayHost.Initialize(args);
        _captureOverlayHost.Activate();
    }

    private async Task DisposeCaptureOverlayHostAsync()
    {
        if (_captureOverlayHost == null)
        {
            return;
        }

        try
        {
            _captureOverlayHost.Close();
            _captureOverlayHost.Dispose();
        }
        catch { }
        finally
        {
            _captureOverlayHost = null;
        }

        // Force garbage collection
        await Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect();
        });
    }

    private void OnSelectionOverlayHostLostFocus(object? sender, EventArgs e)
    {
        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            _shutdownHandler.Shutdown();
        }
    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost.Handle;
    }
}
