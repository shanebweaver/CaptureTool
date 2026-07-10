using CommunityToolkit.Mvvm.Input;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using Rectangle = System.Drawing.Rectangle;

namespace CaptureTool.Presentation.Windows.WinUI.Capture;

internal sealed class CaptureDiscardConfirmationOverlayHost : IDisposable
{
    private const string WindowClassName = "CaptureDiscardConfirmationOverlayWindow";

    private readonly Rectangle? _windowBounds;
    private readonly TaskCompletionSource<bool> _resultCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private HWND? _hwnd;
    private DesktopWindowXamlSource? _xamlSource;
    private Grid? _root;
    private bool _disposed;

    public CaptureDiscardConfirmationOverlayHost(Rectangle? windowBounds = null)
    {
        _windowBounds = windowBounds;
    }

    public async Task<bool> ShowConfirmationAsync(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        Initialize(title, content, discardButtonText, cancelButtonText);
        Activate();

        try
        {
            return await _resultCompletion.Task;
        }
        finally
        {
            Dispose();
        }
    }

    private void Initialize(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        if (_hwnd is not null)
        {
            return;
        }

        Rectangle bounds = _windowBounds ?? GetFallbackWindowBounds();
        _hwnd = CreateOverlayWindow(bounds);

        _xamlSource = new DesktopWindowXamlSource();
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd.Value);
        _xamlSource.Initialize(windowId);

        _root = CreateRoot(title, content, discardButtonText, cancelButtonText);
        _xamlSource.Content = _root;
    }

    private static unsafe HWND CreateOverlayWindow(Rectangle bounds)
    {
        WNDCLASSEXW wndClass = new()
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            style = 0,
            lpfnWndProc = &WindowProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = HINSTANCE.Null,
            hIcon = HICON.Null,
            hCursor = HCURSOR.Null,
            hbrBackground = HBRUSH.Null,
            lpszMenuName = null,
            hIconSm = HICON.Null
        };
        fixed (char* name = WindowClassName)
        {
            wndClass.lpszClassName = name;
            PInvoke.RegisterClassEx(in wndClass);
        }

        HWND hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            WindowClassName,
            null,
            WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_POPUP,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            new(IntPtr.Zero),
            null,
            null,
            null);

        PInvoke.SetWindowDisplayAffinity(hwnd, WINDOW_DISPLAY_AFFINITY.WDA_EXCLUDEFROMCAPTURE);

        return hwnd;
    }

    private Grid CreateRoot(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(Colors.Transparent)
        };
        root.PointerPressed += Root_PointerPressed;

        UIElement card = CreateConfirmationCard(title, content, discardButtonText, cancelButtonText);
        root.Children.Add(card);

        return root;
    }

    private ConfirmationCard CreateConfirmationCard(
        string title,
        string content,
        string discardButtonText,
        string cancelButtonText)
    {
        return new ConfirmationCard
        {
            Title = title,
            Message = content,
            ConfirmButtonText = discardButtonText,
            CancelButtonText = cancelButtonText,
            ConfirmCommand = new RelayCommand(() => Complete(true)),
            CancelCommand = new RelayCommand(() => Complete(false))
        };
    }

    private void Activate()
    {
        if (_hwnd is null)
        {
            return;
        }

        Win32WindowHelpers.SetActiveWindow(_hwnd.Value);
        Win32WindowHelpers.SetForegroundWindow(_hwnd.Value);
    }

    private void Root_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, _root))
        {
            Complete(false);
        }
    }

    private void Complete(bool shouldDiscard)
    {
        _resultCompletion.TrySetResult(shouldDiscard);
    }

    private static Rectangle GetFallbackWindowBounds()
    {
        HWND foregroundWindow = PInvoke.GetForegroundWindow();
        HMONITOR monitor = PInvoke.MonitorFromWindow(foregroundWindow, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        MONITORINFO info = new()
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>()
        };
        PInvoke.GetMonitorInfo(monitor, ref info);

        return new Rectangle(
            info.rcMonitor.left,
            info.rcMonitor.top,
            info.rcMonitor.right - info.rcMonitor.left,
            info.rcMonitor.bottom - info.rcMonitor.top);
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _resultCompletion.TrySetResult(false);

        if (_root is not null)
        {
            _root.PointerPressed -= Root_PointerPressed;
            _root = null;
        }

        if (_xamlSource is not null)
        {
            try
            {
                _xamlSource.Content = null;
                (_xamlSource as IDisposable)?.Dispose();
            }
            catch
            {
            }
            _xamlSource = null;
        }

        if (_hwnd is not null)
        {
            try
            {
                PInvoke.DestroyWindow(_hwnd.Value);
            }
            catch
            {
            }
            _hwnd = null;
        }
    }
}
