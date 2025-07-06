using CaptureTool.Capture;
using CaptureTool.Services.Themes;
using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class CaptureOverlayWindow : Window
{
    public CaptureOverlayWindowViewModel ViewModel { get; } = ViewModelLocator.GetViewModel<CaptureOverlayWindowViewModel>();

    public CaptureOverlayWindow(MonitorCaptureResult monitor)
    {
        Activated += CaptureOverlayWindow_Activated;

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        ViewModel.Monitor = monitor;
        ViewModel.CloseRequested += ViewModel_CloseRequested;

        var bounds = monitor.MonitorBounds;
        AppWindow.MoveAndResize(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
        }

        InitializeComponent();
        ToolbarPanel.Loaded += ToolbarPanel_Loaded;

        UpdateRequestedAppTheme();
        LoadBackgroundImage();
    }

    private void LoadBackgroundImage()
    {
        if (ViewModel.Monitor is not null && RootPanel is not null)
        {
            var monitor = ViewModel.Monitor;
            var monitorBounds = monitor.MonitorBounds;

            var writeableBitmap = new WriteableBitmap(monitorBounds.Width, monitorBounds.Height);
            using (var stream = writeableBitmap.PixelBuffer.AsStream())
            {
                stream.Write(monitor.PixelBuffer, 0, monitor.PixelBuffer.Length);
            }

            RootPanel.Background = new Microsoft.UI.Xaml.Media.ImageBrush
            {
                ImageSource = writeableBitmap,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            };
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

        RootPanel.SetValue(FrameworkElement.RequestedThemeProperty, theme);
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

    private void ViewModel_CloseRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void CaptureOverlayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        ViewModel.IsActive = args.WindowActivationState != WindowActivationState.Deactivated;
        if (ViewModel.IsActive && ViewModel.IsPrimary)
        {
            // Must call SetForegroundWindow or focus will not move to the new window on activation.
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            global::Windows.Win32.PInvoke.SetForegroundWindow(new(hwnd));
        }
    }

    private void ToolbarPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ToolbarPanel.Loaded -= ToolbarPanel_Loaded;

        if (ViewModel.IsPrimary)
        {
            ToolbarPanel.Visibility = Visibility.Visible;
        }
    }

    private void SelectionOverlay_SelectionComplete(object sender, Rectangle captureArea)
    {
        ViewModel.CaptureArea = captureArea;

        if (captureArea.Height >= 40 && captureArea.Width >= 40)
        {
            ViewModel.RequestCaptureCommand.Execute(null);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseOverlayCommand.Execute(null);
    }
}
