using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.Navigation;
using CaptureTool.Application.Features.Windowing.ShowMainWindow;
using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Navigation;
using CaptureTool.Infrastructure.Abstractions.Shutdown;
using CaptureTool.Infrastructure.Abstractions.Windowing;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

namespace CaptureTool.Presentation.Windows.WinUI;

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
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly INavigationService _navigationService;
    private readonly ShowMainWindowUseCase _showMainWindowCommand;

    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private SelectionOverlayHost? _selectionOverlayHost;
    private CaptureOverlayHost? _captureOverlayHost;
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public AppNavigationHandler(
        IShutdownHandler shutdownHandler,
        IVideoCaptureHandler videoCaptureHandler,
        INavigationService navigationService,
        ShowMainWindowUseCase showMainWindowCommand)
    {
        _shutdownHandler = shutdownHandler;
        _videoCaptureHandler = videoCaptureHandler;
        _navigationService = navigationService;
        _showMainWindowCommand = showMainWindowCommand;
    }

    public async void HandleNavigationRequest(INavigationRequest request)
    {
        await _semaphoreNavigation.WaitAsync();

        _mainWindowHost.Initialize();

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
            else if (request.Route is NavigationRoute imageRoute && imageRoute == NavigationRoute.SelectionOverlay)
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
            else if (request.Route is NavigationRoute videoRoute && videoRoute == NavigationRoute.CaptureOverlay)
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
        if (_navigationService.CanGoBack)
        {
            _showMainWindowCommand.ExecuteAsync(new ShowMainWindowRequest()).GetAwaiter().GetResult();
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
