using CaptureTool.UI.Windows.Utils;
using CaptureTool.ViewModels;
using Microsoft.UI.Xaml;
using System.Drawing;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class SelectionOverlayWindow : Window
{
    public SelectionOverlayWindowViewModel ViewModel => RootView.ViewModel;

    public Rectangle MonitorBounds { get; private set; }
    public bool IsClosed { get; private set; }

    public SelectionOverlayWindow(SelectionOverlayWindowOptions overlayOptions)
    {
        Activated += OnActivated;
        Closed += OnClosed;
        if (overlayOptions.Monitor.IsPrimary)
        {
            Activated += OnPrimaryActivated;
            Closed += OnPrimaryClosed;
        }

        MonitorBounds = overlayOptions.Monitor.MonitorBounds;
        
        EnsureMaximized();
        InitializeComponent();

        ViewModel.Load(overlayOptions);
    }

    private void EnsureMaximized()
    {
        try
        {
            this.MoveAndResize(MonitorBounds);
            this.MakeBorderlessOverlay();
        }
        catch { }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            return;
        }

        EnsureMaximized();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnActivated;
        Closed -= OnClosed;

        ViewModel.Dispose();

        Content = null;
        IsClosed = true;
    }

    private void OnPrimaryActivated(object sender, WindowActivatedEventArgs args)
    {
        try
        {
            if (!IsClosed && args.WindowActivationState != WindowActivationState.Deactivated)
            {
                // Must call SetForegroundWindow or focus will not move to the new window on activation.
                this.SetForegroundWindow();
                this.ExcludeFromScreenCapture();
            }
        }
        catch
        {
        }
    }

    private void OnPrimaryClosed(object sender, WindowEventArgs args)
    {
        Activated -= OnPrimaryActivated;
        Closed -= OnPrimaryClosed;
    }
}
