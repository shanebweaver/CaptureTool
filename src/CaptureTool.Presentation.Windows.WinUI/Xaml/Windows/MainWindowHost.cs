using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Presentation.Windows.WinUI.Utils;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

internal sealed partial class MainWindowHost : INavigationHandler, IDisposable
{
    private MainWindow? _mainWindow;

    public nint Handle => _mainWindow?.GetWindowHandle() ?? IntPtr.Zero;

    public void Initialize()
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
    }

    public void Hide()
    {
        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow == null)
            {
                return;
            }

            _mainWindow.AppWindow.Hide();
        });
    }

    public void Dispose()
    {
        _mainWindow = null;
    }

    public void HandleNavigationRequest(INavigationRequest request)
    {
        _mainWindow?.ViewModel.HandleNavigationRequest(request);
    }
}