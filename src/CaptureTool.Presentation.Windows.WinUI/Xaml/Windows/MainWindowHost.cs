using CaptureTool.Infrastructure.Interfaces.Navigation;
using CaptureTool.Presentation.Windows.WinUI.Utils;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

internal sealed partial class MainWindowHost : INavigationHandler, IDisposable
{
    private MainWindow? _mainWindow;

    public nint Handle => _mainWindow?.GetWindowHandle() ?? IntPtr.Zero;

    private void EnsureCreated()
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
        EnsureCreated();

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow == null)
            {
                return;
            }

            _mainWindow.Restore();
            _mainWindow.Activate();
            _mainWindow.SetForegroundWindow();
        });
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