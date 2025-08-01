using CaptureTool.UI.Windows.Xaml.Extensions;
using System;
using Windows.Win32;
using WinRT.Interop;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class MainWindowHost : IDisposable
{
    private MainWindow? _mainWindow;
    private readonly Action _onClosed;

    public MainWindowHost(Action onClosed)
    {
        _onClosed = onClosed;
    }

    public nint Handle => _mainWindow is not null
        ? WindowNative.GetWindowHandle(_mainWindow)
        : IntPtr.Zero;

    public void EnsureCreated()
    {
        if (_mainWindow != null)
        {
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Closed += (_, _) =>
        {
            CleanupMainWindow();
            _onClosed?.Invoke();
        };
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
        CleanupMainWindow();
    }

    private void CleanupMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Close();
            _mainWindow = null;
        }
    }
}