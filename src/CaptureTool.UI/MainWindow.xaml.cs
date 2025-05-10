using System;
using System.Threading;
using CaptureTool.Core;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using CaptureTool.UI.Xaml.Pages;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage;
using WinRT.Interop;

namespace CaptureTool.UI;

public sealed partial class MainWindow : Window
{
    private const string MainWindow_X = "MainWindow_X";
    private const string MainWindow_Y = "MainWindow_Y";
    private const string MainWindow_Width = "MainWindow_Width";
    private const string MainWindow_Height = "MainWindow_Height";

    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();

    private readonly CancellationTokenSource _activationCts = new();

    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        InitializeComponent();

        _appWindow = GetAppWindowForCurrentWindow();
        _appWindow.Closing += OnAppWindowClosing;
        
        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;

        Activated += OnActivated;
        Closed += OnClosed;
        VisibilityChanged += OnVisibilityChanged;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
        ViewModel.PresentationUpdateRequested += OnViewModelPresentationUpdateRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        SaveAppWindowSizeAndPosition();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentAppTheme))
        {
            UpdateRequestedAppTheme();
        }
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
            RestoreAppWindowSizeAndPosition();
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

    private void OnVisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        // TODO: Figure out how this affects the size. We don't want to restore a minimized window to the previous size (0,0).
    }

    private void OnViewModelNavigationRequested(object? sender, NavigationRequest navigationRequest)
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

    private void OnViewModelPresentationUpdateRequested(object? sender, AppWindowPresenterAction action)
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

    private void RestoreAppWindowSizeAndPosition()
    {
        var data = ApplicationData.GetDefault().LocalSettings;
        var appWindowRect = new Windows.Graphics.RectInt32(
            (data.Values.TryGetValue(MainWindow_X, out object? oX) && (oX is int x) && x >= 0) ? x : 48,
            (data.Values.TryGetValue(MainWindow_Y, out object? oY) && (oY is int y) && y >= 0) ? y : 48,
            (data.Values.TryGetValue(MainWindow_Width, out object? oW) && (oW is int w) && w > 0) ? w : 540,
            (data.Values.TryGetValue(MainWindow_Height, out object? oH) && (oH is int h) && h > 0) ? h : 320);

        AppWindow.MoveAndResize(appWindowRect, DisplayArea.Primary);
    }

    private void SaveAppWindowSizeAndPosition()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            var data = ApplicationData.GetDefault().LocalSettings;
            bool isMaximized = presenter.State == OverlappedPresenterState.Maximized;
            bool isMinimized = presenter.State == OverlappedPresenterState.Minimized;
            if (!isMinimized && !isMaximized)
            {
                data.Values[MainWindow_X] = AppWindow.Position.X;
                data.Values[MainWindow_Y] = AppWindow.Position.Y;
                data.Values[MainWindow_Width] = AppWindow.Size.Width;
                data.Values[MainWindow_Height] = AppWindow.Size.Height;
            }
            else
            {
                data.Values[MainWindow_X] = null;
                data.Values[MainWindow_Y] = null;
                data.Values[MainWindow_Width] = null;
                data.Values[MainWindow_Height] = null;
            }
        }
    }

    private void UpdateRequestedAppTheme()
    {
        object theme = ViewModel.CurrentAppTheme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            AppTheme.SystemDefault => ConvertToElementTheme(ViewModel.DefaultAppTheme),
            _ => DependencyProperty.UnsetValue
        };

        RootGrid.SetValue(FrameworkElement.RequestedThemeProperty, theme);
    }

    private static ElementTheme ConvertToElementTheme(AppTheme appTheme)
    {
        return appTheme switch
        {
            AppTheme.SystemDefault => ElementTheme.Default,
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }
}
