using System;
using CaptureTool.Core;
using CaptureTool.Services.AppController;
using CaptureTool.Services.Logging;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.SnippingTool;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace CaptureTool.UI;

internal class CaptureToolAppController : IAppController
{
    private readonly ILogService _logService;
    private readonly INavigationService _navigationService;
    private readonly ISnippingToolService _snippingToolService;

    public event EventHandler<AppWindowPresenterAction>? AppWindowPresentationUpdateRequested;

    public CaptureToolAppController(
        ILogService logService,
        INavigationService navigationService,
        ISnippingToolService snippingToolService) 
    {
        _logService = logService;
        _navigationService = navigationService;
        _snippingToolService = snippingToolService;
    }

    public bool TryRestart()
    {
        AppRestartFailureReason restartError = AppInstance.Restart(string.Empty);

        switch (restartError)
        {
            case AppRestartFailureReason.NotInForeground:
                _logService.LogWarning("The app is not in the foreground.");
                break;
            case AppRestartFailureReason.RestartPending:
                _logService.LogWarning("Another restart is currently pending.");
                break;
            case AppRestartFailureReason.InvalidUser:
                _logService.LogWarning("Current user is not signed in or not a valid user.");
                break;
            case AppRestartFailureReason.Other:
                _logService.LogWarning("Failure restarting.");
                break;
        }

        return false;
    }

    public void Shutdown()
    {
        App.Current.Shutdown();
    }

    public async void NewDesktopCapture()
    {
        // TODO: Check the feature state

        // Show loading screen
        _navigationService.Navigate(NavigationRoutes.Loading, null);
        UpdateAppWindowPresentation(AppWindowPresenterAction.Minimize);
        await _snippingToolService.CaptureImageAsync();
    }

    public void NewCameraCapture()
    {

    }

    public void NewAudioCapture()
    {

    }

    public void UpdateAppWindowPresentation(AppWindowPresenterAction action)
    {
        AppWindowPresentationUpdateRequested?.Invoke(this, action);
    }
}
