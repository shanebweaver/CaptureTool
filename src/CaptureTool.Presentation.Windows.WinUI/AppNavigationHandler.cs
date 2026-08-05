using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.Windowing;
using CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;
using CaptureTool.Domain.Capture;
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
    private readonly ICancelVideoCaptureUseCase _cancelVideoCaptureCommand;
    private readonly IShowMainWindowUseCase _showMainWindowCommand;

    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private SelectionOverlayHost? _selectionOverlayHost;
    private CaptureOverlayHost? _captureOverlayHost;
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public AppNavigationHandler(
        IShutdownHandler shutdownHandler,
        ICancelVideoCaptureUseCase cancelVideoCaptureCommand,
        IShowMainWindowUseCase showMainWindowCommand)
    {
        _shutdownHandler = shutdownHandler;
        _cancelVideoCaptureCommand = cancelVideoCaptureCommand;
        _showMainWindowCommand = showMainWindowCommand;
    }

    public async Task<NavigationResult> HandleNavigationRequestAsync(
        INavigationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _semaphoreNavigation.WaitAsync(cancellationToken);

        try
        {
            if (CaptureToolNavigationRouteHelper.IsMainWindowRoute(request.Route))
            {
                UXHost previousHost = _activeHost;
                if (previousHost == UXHost.CaptureOverlay &&
                    !await TryCancelVideoCaptureAsync(cancellationToken))
                {
                    return NavigationResult.Rejected;
                }

                NavigationResult mainWindowResult = await _mainWindowHost.HandleNavigationRequestAsync(
                    request,
                    cancellationToken);
                if (mainWindowResult == NavigationResult.Rejected)
                {
                    return mainWindowResult;
                }

                if (previousHost == UXHost.SelectionOverlay)
                {
                    await DisposeSelectionOverlayHostAsync();
                }
                else if (previousHost == UXHost.CaptureOverlay)
                {
                    await DisposeCaptureOverlayHostAsync();
                }

                _mainWindowHost.ExcludeWindowFromCapture(false);
                _mainWindowHost.Show();

                // NoChange means the requested page was already visible in the hidden main window.
                // Switching back from an overlay still accepts the application-level transition.
                _activeHost = UXHost.MainWindow;
                return NavigationResult.Accepted;
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
                        await Task.Delay(200, cancellationToken);
                        break;

                    case UXHost.SelectionOverlay:
                        _selectionOverlayHost?.UpdateOptions(options);
                        return NavigationResult.Accepted;

                    case UXHost.CaptureOverlay:
                        if (!await TryCancelVideoCaptureAsync(cancellationToken))
                        {
                            return NavigationResult.Rejected;
                        }

                        await DisposeCaptureOverlayHostAsync();
                        break;
                }

                // Create fresh instance using factory pattern
                await CreateSelectionOverlayHostAsync(options);
                _activeHost = UXHost.SelectionOverlay;
                return NavigationResult.Accepted;
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
                        await Task.Delay(200, cancellationToken);
                        break;

                    case UXHost.SelectionOverlay:
                        await DisposeSelectionOverlayHostAsync();
                        break;

                    case UXHost.CaptureOverlay:
                        return NavigationResult.Rejected;
                }

                // Create fresh instance using factory pattern
                await CreateCaptureOverlayHostAsync(args);
                _activeHost = UXHost.CaptureOverlay;
                return NavigationResult.Accepted;
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

    private async Task<bool> TryCancelVideoCaptureAsync(CancellationToken cancellationToken)
    {
        var response = await _cancelVideoCaptureCommand.ExecuteAsync(
            new CancelVideoCaptureRequest(),
            cancellationToken);
        return response.Value?.Succeeded == true;
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

    private async void OnSelectionOverlayHostLostFocus(object? sender, EventArgs e)
    {
        var response = await _showMainWindowCommand.ExecuteAsync(
            new ShowMainWindowRequest(CreateIfUnavailable: false));

        if (response.Value?.Succeeded != true)
        {
            _shutdownHandler.Shutdown();
        }
    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost.Handle;
    }
}
