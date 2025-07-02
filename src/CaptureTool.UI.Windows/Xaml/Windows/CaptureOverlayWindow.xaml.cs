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
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        ViewModel.Monitor = monitor;

        var bounds = monitor.MonitorBounds;
        AppWindow.MoveAndResize(new(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.Maximize();
        }

        if (ViewModel.IsPrimary)
        {
            ToolbarPanel.Visibility = Visibility.Visible;
            ToolbarPanel.Loaded += ToolbarPanel_Loaded;
        }
    }

    private void ToolbarPanel_Loaded(object sender, RoutedEventArgs e)
    {
        ToolbarPanel.Focus(FocusState.Programmatic);
    }

    private void SelectionOverlay_SelectionComplete(object sender, EventArgs e)
    {
        if (ViewModel.CaptureArea.Height >= 40 && ViewModel.CaptureArea.Width >= 40)
        {
            _ = PerformCaptureAsync();
        }
        else
        {
            ViewModel.CaptureArea = Rectangle.Empty;
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
}
