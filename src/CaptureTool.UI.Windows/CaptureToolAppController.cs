using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Domains.Capture.Interfaces;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Logging;
using CaptureTool.Services.Interfaces.Navigation;
using CaptureTool.UI.Windows.Xaml.Windows;
using Microsoft.Windows.AppLifecycle;
using System.Diagnostics;

namespace CaptureTool.UI.Windows;

internal partial class CaptureToolAppController : IAppController
{
    private enum UXHost
    {
        None,
        MainWindow,
        SelectionOverlay,
        CaptureOverlay
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ILogService _logService;
    private readonly IVideoCaptureHandler _videoCaptureHandler;
    private readonly ICancellationService _cancellationService;

    private readonly SemaphoreSlim _semaphoreNavigation = new(1, 1);
    private readonly SelectionOverlayHost _selectionOverlayHost = new();
    private readonly CaptureOverlayHost _captureOverlayHost = new();
    private readonly MainWindowHost _mainWindowHost = new();

    private UXHost _activeHost;

    public CaptureToolAppController(
        IAppNavigation appNavigation,
        ILogService logService,
        IVideoCaptureHandler videoCaptureHandler,
        ICancellationService cancellationService) 
    {
        _appNavigation = appNavigation;
        _logService = logService;
        _videoCaptureHandler = videoCaptureHandler;
        _cancellationService = cancellationService;
    }

    private void OnSelectionOverlayHostLostFocus(object? sender, EventArgs e)
    {
        if (_appNavigation.CanGoBack)
        {
            _appNavigation.GoBackToMainWindow();
        }
        else
        {
            Shutdown();
        }
    }

    #region INavigationHandler
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
    #endregion

    public bool TryRestart()
    {
        global::Windows.ApplicationModel.Core.AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case global::Windows.ApplicationModel.Core.AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public void Shutdown()
    {
        lock (this)
        {
            try
            {
                _captureOverlayHost.Dispose();
                _selectionOverlayHost.Dispose();
                _mainWindowHost.Dispose();
                _cancellationService.CancelAll();
            }
            catch (Exception e)
            {
                Debug.Fail($"Error during shutdown: {e.Message}");
            }

            Environment.Exit(0);
        }
    }

    public nint GetMainWindowHandle()
    {
        return _mainWindowHost.Handle;
    }

    public string GetDefaultScreenshotsFolderPath()
    {
        return global::Windows.Storage.KnownFolders.SavedPictures.Path;
    }
}
