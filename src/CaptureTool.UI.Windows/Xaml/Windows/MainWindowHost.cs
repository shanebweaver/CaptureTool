using CaptureTool.Services.Navigation;
using CaptureTool.UI.Windows.Xaml.Extensions;
using System;
using Windows.Win32;
using WinRT.Interop;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class MainWindowHost : INavigationHandler, IDisposable
{
    private MainWindow? _mainWindow;

    public nint Handle => _mainWindow is not null
        ? WindowNative.GetWindowHandle(_mainWindow)
        : IntPtr.Zero;

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
        if (_mainWindow != null)
        {
            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            var displayAffinity = exclude 
                ? global::Windows.Win32.UI.WindowsAndMessaging.WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE 
                : global::Windows.Win32.UI.WindowsAndMessaging.WINDOW_DISPLAY_AFFINITY.WDA_NONE;
            PInvoke.SetWindowDisplayAffinity(new(hwnd), displayAffinity);
        }
    }

    public void Restore()
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

            var hwnd = WindowNative.GetWindowHandle(_mainWindow);
            PInvoke.SetForegroundWindow(new(hwnd));
        });
    }

    public void Show(bool activate = true)
    {
        EnsureCreated();

        App.Current.DispatcherQueue.TryEnqueue(() =>
        {
            if (_mainWindow == null)
            {
                return;
            }

            _mainWindow.AppWindow.Show(activate);

            if (activate)
            {
                var hwnd = WindowNative.GetWindowHandle(_mainWindow);
                PInvoke.SetForegroundWindow(new(hwnd));
            }
        });
    }

    public void Hide()
    {
        if (_mainWindow == null)
        {
            return;
        }

        _mainWindow.AppWindow.Hide();
    }

    public void Dispose()
    {
        _mainWindow = null;
    }

    public void HandleNavigationRequest(NavigationRequest request)
    {
        Show();
        _mainWindow?.ViewModel.HandleNavigationRequest(request);
    }
}