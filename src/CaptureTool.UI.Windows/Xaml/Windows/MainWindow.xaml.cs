using CaptureTool.Core.AppController;
using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using CaptureTool.UI.Windows.Xaml.Pages;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage;
using System;
using System.Threading;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly System.Drawing.Rectangle DefaultWindowRect = new(48, 48, 540, 320);

    private const string MainWindow_X = "MainWindow_X";
    private const string MainWindow_Y = "MainWindow_Y";
    private const string MainWindow_Width = "MainWindow_Width";
    private const string MainWindow_Height = "MainWindow_Height";

    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();

    private readonly CancellationTokenSource _activationCts = new();

    public MainWindow()
    {
        InitializeComponent();

        AppWindow.Closing += OnAppWindowClosing;

        // TODO: Fix colors in the titlebar.
        // TODO: Make sure the colors are theme aware.
        //AppWindow.TitleBar.BackgroundColor = Colors.Red;

        Activated += OnActivated;
        Closed += OnClosed;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
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

        _activationCts.Cancel();
        _activationCts.Dispose();
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

    private void RestoreAppWindowSizeAndPosition()
    {
        var data = ApplicationData.GetDefault().LocalSettings;
        var appWindowRect = new global:: Windows.Graphics.RectInt32(
            (data.Values.TryGetValue(MainWindow_X, out object? oX) && (oX is int x) && x >= 0) ? x : DefaultWindowRect.X,
            (data.Values.TryGetValue(MainWindow_Y, out object? oY) && (oY is int y) && y >= 0) ? y : DefaultWindowRect.Y,
            (data.Values.TryGetValue(MainWindow_Width, out object? oW) && (oW is int w) && w > 0) ? w : DefaultWindowRect.Width,
            (data.Values.TryGetValue(MainWindow_Height, out object? oH) && (oH is int h) && h > 0) ? h : DefaultWindowRect.Height);

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
