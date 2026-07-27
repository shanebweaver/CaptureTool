using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Presentation.Windows.WinUI.Utils;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

internal sealed partial class MainWindowHost : INavigationHandler, IDisposable
{
    private MainWindow? _mainWindow;

    public nint Handle => _mainWindow?.GetWindowHandle() ?? IntPtr.Zero;

    private void Initialize()
    {
        if (_mainWindow != null)
        {
            return;
        }

        _mainWindow = new MainWindow();
    }

    public void ExcludeWindowFromCapture(bool exclude)
    {
        if (exclude)
        {
            _mainWindow?.ExcludeFromScreenCapture();
        }
        else
        {
            _mainWindow?.IncludeInScreenCapture();
        }
    }

    public void Show()
    {
        // Keep construction lazy so protocol-only capture activations cannot load a hidden
        // main window and display optional dialogs over the selection or capture overlays.
        Initialize();

        if (_mainWindow == null)
        {
            return;
        }

        // Only restore if the window is actually minimized
        // Otherwise, SW_RESTORE will unsnap a snapped window
        if (_mainWindow.IsMinimized())
        {
            _mainWindow.Restore();
        }
        _mainWindow.Activate();
        _mainWindow.SetForegroundWindow();
        _mainWindow.NotifyShown();
    }

    public void Hide()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow == null)
            {
                return;
            }

            _mainWindow.NotifyHidden();
            _mainWindow.SuspendMediaPlayback();
            _mainWindow.AppWindow.Hide();
        });
    }

    public void Dispose()
    {
        _mainWindow = null;
    }

    public void HandleNavigationRequest(INavigationRequest request)
    {
        _mainWindow?.HandleNavigationRequest(request);
    }
}
