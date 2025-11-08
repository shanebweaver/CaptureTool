using CaptureTool.Services.Navigation;
using CaptureTool.Services.Themes;
using CaptureTool.UI.Windows.Xaml.Extensions;
using CaptureTool.UI.Windows.Xaml.Pages;
using CaptureTool.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.WindowManagement;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly SizeInt32 DefaultWindowSize = new(720, 540);

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

            var rightInset = AppWindow.TitleBar.RightInset;
            var leftInset = AppWindow.TitleBar.LeftInset;

            RightPaddingColumn.Width = new GridLength(rightInset);
            LeftPaddingColumn.Width = new GridLength(leftInset);

            int offsetX = (int)GetElementOffsetFromWindowLeftInPixels(DraggablePanel);
            int offsetY = 0; // At the top

            var width = (int)(AppWindow.ClientSize.Width - (rightInset + leftInset + AppMenuColumn.ActualWidth));
            var height = AppWindow.TitleBar.Height;

            RectInt32 draggableRect = new(offsetX, offsetY, width, height);
            AppWindow.TitleBar.SetDragRectangles([draggableRect]);
        });
    }
    
    // Returns the X offset in physical pixels from the element to the left edge of the window
    private static double GetElementOffsetFromWindowLeftInPixels(FrameworkElement element)
    {
        // Transform (0,0) of the element to the window's coordinate space
        GeneralTransform transform = element.TransformToVisual(null); // 'null' means root visual (window)
        Point offset = transform.TransformPoint(new Point(0, 0));
        return offset.X;
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

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            global::Windows.Win32.PInvoke.SetWindowDisplayAffinity(new(hwnd), global::Windows.Win32.UI.WindowsAndMessaging.WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.NavigationRequested -= OnViewModelNavigationRequested;

        _activationCts.Cancel();
        _activationCts.Dispose();

        // IMPORTANT: Closing the main window will crash the app unless we forcefully exit immediately.
        ServiceLocator.AppController.Shutdown();
    }

    private void OnViewModelNavigationRequested(object? sender, NavigationRequest navigationRequest)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Type pageType = PageLocator.GetPageType(navigationRequest.Route);
            if (navigationRequest.IsBackNavigation && NavigationFrame.CanGoBack)
            {
                NavigationFrame.GoBack();
                NavigationFrame.ForwardStack.Clear();
                GC.Collect();
            }
            else
            {
                NavigationFrame.Navigate(pageType, navigationRequest.Parameter);
            }

            if (navigationRequest.ClearHistory)
            {
                NavigationFrame.ForwardStack.Clear();
                NavigationFrame.BackStack.Clear();
                GC.Collect();
            }
        });
    }

    private void RestoreAppWindowSizeAndPosition()
    {
        AppWindow.Move(new PointInt32(1,1));
        AppWindow.Resize(DefaultWindowSize);
        this.CenterOnScreen();
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
