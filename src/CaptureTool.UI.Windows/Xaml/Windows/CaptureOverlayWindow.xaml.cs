using CaptureTool.Capture;
using CaptureTool.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Drawing;
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
            _ = PerformCaptureAsync();
        }
    }

    private async Task PerformCaptureAsync()
    {
        RootPanel.Opacity = 0;

        // Allow the UI thread to process the opacity change and render.
        // This is not ideal, but there is no deterministic way to ensure that the UI is updated in time for the capture.
        await Task.Yield();
        await Task.Yield();
        await Task.Delay(50);

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.RequestCaptureCommand.Execute(null);
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseOverlayCommand.Execute(null);
    }
}
