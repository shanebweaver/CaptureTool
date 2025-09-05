using CaptureTool.Capture;
using System.Drawing;

namespace CaptureTool.UI.Windows.Xaml.Views;

public sealed partial class CaptureOverlayView : CaptureOverlayViewBase
{
    public CaptureOverlayView(MonitorCaptureResult monitor, Rectangle area)
    {
        InitializeComponent();
        ViewModel.Load((monitor, area));
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        ViewModel.Unload();
    }
}
