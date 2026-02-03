using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using Microsoft.UI.Xaml;
using System.Drawing;
using System.Threading;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

public sealed partial class SelectionOverlayWindow : Window
{
    public ISelectionOverlayWindowViewModel ViewModel => RootView.ViewModel;

    public Rectangle MonitorBounds { get; private set; }
    public bool IsClosed { get; private set; }
    private int _windowShownFlag = 0;  // Using int for Interlocked operations (0 = not shown, 1 = shown)

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
        
        // Hide window before initializing to prevent black flash
        this.Hide();
        
        InitializeComponent();

        // Pass window reference to view so it can show when ready
        RootView.SetParentWindow(this);

        ViewModel.Load(overlayOptions);
    }

    public void ShowWindowWhenReady()
    {
        // Use Interlocked for thread-safe check-and-set
        if (Interlocked.CompareExchange(ref _windowShownFlag, 1, 0) == 0 && !IsClosed)
        {
            this.Show();
        }
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

        try
        {
            ViewModel.Dispose();
        }
        catch { }

        try
        {
            if (Content is FrameworkElement rootView)
            {
                rootView.DataContext = null;
            }
            Content = null;
        }
        catch { }

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
