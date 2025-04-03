using System;
using System.Threading;
using CaptureTool.Services.AppController;
using CaptureTool.Services.Navigation;
using CaptureTool.UI.Xaml.Pages;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace CaptureTool.UI;

public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();

    private readonly CancellationTokenSource _activationCts = new();

    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _appWindow = GetAppWindowForCurrentWindow();
        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;

        Activated += OnActivated;
        Closed += OnClosed;
        SizeChanged += OnSizeChanged;
        VisibilityChanged += OnVisibilityChanged;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
        ViewModel.PresentationUpdateRequested += OnViewModelPresentationUpdateRequested;
    }

    private AppWindow GetAppWindowForCurrentWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated && ViewModel.IsUnloaded)
        {
            AppWindow.MoveAndResize(new(48, 48, 540, 320), DisplayArea.Primary);
            await ViewModel.LoadAsync(null, _activationCts.Token);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;
        ViewModel.PresentationUpdateRequested -= OnViewModelPresentationUpdateRequested;

        _activationCts.Cancel();
        _activationCts.Dispose();
    }

    private void OnSizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        //Size newSize = args.Size;
        // TODO: Save size to settings
    }

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        // TODO: Figure out how this affects the size. We don't want to restore a minimized window to the previous size (0,0).
    }

    private void OnViewModelNavigationRequested(NavigationRequest navigationRequest)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (navigationRequest.IsBackNavigation)
            {
                NavigationFrame.GoBack();
            }
            else
            {
                Type pageType = PageLocator.GetPageType(navigationRequest.Route);
                NavigationFrame.Navigate(pageType, navigationRequest.Parameter);
            }
        });
    }

    private void OnViewModelPresentationUpdateRequested(AppWindowPresenterAction action)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (AppWindow.Presenter is OverlappedPresenter presenter)
            {
                switch (action)
                {
                    case AppWindowPresenterAction.Restore:
                        presenter.Restore();
                        break;
                    case AppWindowPresenterAction.Minimize:
                        presenter.Minimize();
                        break;
                    case AppWindowPresenterAction.Maximize:
                        presenter.Maximize();
                        break;
                }
            }
        });
    }
}
