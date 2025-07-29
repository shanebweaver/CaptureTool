using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Pages;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage;
using System;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly SizeInt32 DefaultWindowSize = new(720, 540);

    private const string MainWindow_X = "MainWindow_X";
    private const string MainWindow_Y = "MainWindow_Y";
    private const string MainWindow_Width = "MainWindow_Width";
    private const string MainWindow_Height = "MainWindow_Height";

    public MainWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<MainWindowViewModel>();

    private readonly CancellationTokenSource _activationCts = new();

    public MainWindow()
    {
        InitializeComponent();

        AppTitleBar.Loaded += AppTitleBar_Loaded;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
        ExtendsContentIntoTitleBar = true;
        UpdateAppTitle();

        Activated += OnActivated;
        Closed += OnClosed;
        ViewModel.NavigationRequested += OnViewModelNavigationRequested;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateRequestedAppTheme();
        UpdateTitleBarColors();

        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        SaveAppWindowSizeAndPosition();
    }

    private void UpdateAppTitle()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            string appTitle = "Capture Tool";

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                appTitle = AppInfo.Current.DisplayInfo.DisplayName;
            }

            Title = appTitle;
            TitleBarTextBlock.Text = appTitle;
        });
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Set the initial interactive regions.
            SetRegionsForCustomTitleBar();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Update interactive regions if the size of the window changes.
            SetRegionsForCustomTitleBar();
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            double scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

            RightPaddingColumn.Width = new GridLength(AppWindow.TitleBar.RightInset / scaleAdjustment);
            LeftPaddingColumn.Width = new GridLength(AppWindow.TitleBar.LeftInset / scaleAdjustment);

            int offsetX = (int)GetElementOffsetFromWindowLeftInPixels(DraggablePanel);
            int offsetY = 0; // At the top

            RectInt32 draggableRect = new(offsetX, offsetY, (int)DraggablePanel.ActualWidth, (int)DraggablePanel.ActualHeight);
            AppWindow.TitleBar.SetDragRectangles([draggableRect]);
        });
    }
    
    // Returns the X offset in physical pixels from the element to the left edge of the window
    private static double GetElementOffsetFromWindowLeftInPixels(FrameworkElement element)
    {
        // Transform (0,0) of the element to the window's coordinate space
        GeneralTransform transform = element.TransformToVisual(null); // 'null' means root visual (window)
        Point offset = transform.TransformPoint(new Point(0, 0));

        // Convert DIPs to physical pixels using the current scale
        double scale = 1;// DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;

        return offset.X * scale;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentAppTheme))
        {
            UpdateRequestedAppTheme();
            UpdateTitleBarColors();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated)
        {
            Activated -= OnActivated;
            RestoreAppWindowSizeAndPosition();
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
        var appWindowSize = new SizeInt32(
            (data.Values.TryGetValue(MainWindow_Width, out object? oW) && (oW is int w) && w > 0) ? w : DefaultWindowSize.Width,
            (data.Values.TryGetValue(MainWindow_Height, out object? oH) && (oH is int h) && h > 0) ? h : DefaultWindowSize.Height);

        AppWindow.Resize(appWindowSize);

        if ((data.Values.TryGetValue(MainWindow_X, out object? oX) && (oX is int x) && x >= 0) &&
            (data.Values.TryGetValue(MainWindow_Y, out object? oY) && (oY is int y) && y >= 0))
        {
            AppWindow.Move(new PointInt32(x, y));
        }
        else
        {
            this.CenterOnScreen();
        }
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

    private void UpdateTitleBarColors()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var titleBar = AppWindow.TitleBar;

            if (titleBar is null)
                return;

            AppTheme appTheme = ViewModel.CurrentAppTheme == AppTheme.SystemDefault
                ? ViewModel.DefaultAppTheme
                : ViewModel.CurrentAppTheme;

            Color foregroundColor = appTheme switch
            {
                AppTheme.Dark => Colors.White,
                _ => Colors.Black,
            };

            titleBar.ForegroundColor = foregroundColor;
            titleBar.ButtonForegroundColor = foregroundColor;

            titleBar.InactiveForegroundColor = foregroundColor;
            titleBar.ButtonInactiveForegroundColor = foregroundColor;
        });
    }
}
