using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Win32.SafeHandles;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

public sealed class SelectionOverlayWindow : IDisposable
{
    internal sealed class DestroyIconSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DestroyIconSafeHandle(HINSTANCE hINSTANCE) : base(true)
        {
            handle = hINSTANCE;
        }

        protected override bool ReleaseHandle()
        {
            return PInvoke.DestroyIcon(new(handle));
        }
    }

    private HWND _hwnd;
    private DesktopWindowXamlSource? _xamlSource;
    private SelectionOverlayWindowView? _view;
    private readonly Rectangle _monitorBounds;
    private readonly bool _isPrimary;
    private int _windowShownFlag = 0;
    private bool _isClosed = false;
    private bool _disposed = false;

    public ISelectionOverlayWindowViewModel ViewModel => _view?.ViewModel!;
    public Rectangle MonitorBounds => _monitorBounds;
    public bool IsClosed => _isClosed;

    public SelectionOverlayWindow(SelectionOverlayWindowOptions overlayOptions)
    {
        _monitorBounds = overlayOptions.Monitor.MonitorBounds;
        _isPrimary = overlayOptions.Monitor.IsPrimary;

        // Create the Win32 window initially hidden
        _hwnd = CreateWindow();

        // Initialize XAML content
        InitializeXamlContent(overlayOptions);
    }

    private unsafe HWND CreateWindow()
    {
        const string className = "SelectionOverlayWindow";

        // Register window class
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
        fixed (char* name = className)
        {
            wndClass.lpszClassName = name;
        }
        PInvoke.RegisterClassEx(in wndClass);

        // Create window WITHOUT WS_VISIBLE flag - window starts hidden
        HWND hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            className,
            null,
            WINDOW_STYLE.WS_POPUP, // No WS_VISIBLE - starts hidden
            _monitorBounds.X,
            _monitorBounds.Y,
            _monitorBounds.Width,
            _monitorBounds.Height,
            new(IntPtr.Zero),
            null,
            new DestroyIconSafeHandle(wndClass.hInstance),
            null);

        // Apply borderless styles without showing
        ApplyBorderlessStyles(hwnd);
        
        // Set position
        Win32WindowHelpers.MoveAndResize(hwnd, _monitorBounds);

        if (_isPrimary)
        {
            Win32WindowHelpers.ExcludeFromScreenCapture(hwnd);
        }

        return hwnd;
    }

    private void ApplyBorderlessStyles(HWND hwnd)
    {
        // Remove standard window chrome (caption, borders, sysmenu)
        var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~(int)(
            WINDOW_STYLE.WS_CAPTION |
            WINDOW_STYLE.WS_THICKFRAME |
            WINDOW_STYLE.WS_SYSMENU |
            WINDOW_STYLE.WS_MINIMIZEBOX |
            WINDOW_STYLE.WS_MAXIMIZEBOX);
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, style);

        // Hide from taskbar and Alt+Tab
        var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= (int)WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        exStyle &= ~(int)WINDOW_EX_STYLE.WS_EX_APPWINDOW;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);

        // Force the system to re-apply new styles WITHOUT showing
        PInvoke.SetWindowPos(
            hwnd,
            new HWND(-1), // HWND_TOPMOST
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    private void InitializeXamlContent(SelectionOverlayWindowOptions overlayOptions)
    {
        // Create DesktopWindowXamlSource to host XAML content
        _xamlSource = new DesktopWindowXamlSource();
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _xamlSource.Initialize(windowId);

        // Create and set the view
        _view = new SelectionOverlayWindowView();
        _xamlSource.Content = _view;

        // Set parent window reference and load view model
        _view.SetParentWindow(this);
        ViewModel.Load(overlayOptions);
    }

    public void ShowWindowWhenReady()
    {
        // Use Interlocked for thread-safe check-and-set
        if (Interlocked.CompareExchange(ref _windowShownFlag, 1, 0) == 0 && !_isClosed)
        {
            Win32WindowHelpers.ShowWindow(_hwnd);
            if (_isPrimary)
            {
                Win32WindowHelpers.SetForegroundWindow(_hwnd);
            }
        }
    }

    public void Activate()
    {
        if (!_isClosed)
        {
            Win32WindowHelpers.SetActiveWindow(_hwnd);
            Win32WindowHelpers.SetForegroundWindow(_hwnd);
        }
    }

    public nint GetWindowHandle()
    {
        return _hwnd;
    }

    public void Close()
    {
        if (_isClosed)
        {
            return;
        }

        _isClosed = true;

        try
        {
            ViewModel?.Dispose();
        }
        catch { }

        if (_view != null)
        {
            _view = null;
        }

        if (_xamlSource != null)
        {
            try
            {
                _xamlSource.Content = null;
                (_xamlSource as IDisposable)?.Dispose();
                _xamlSource = null;
            }
            catch { }
        }

        if (_hwnd.Value != IntPtr.Zero)
        {
            try
            {
                PInvoke.DestroyWindow(_hwnd);
            }
            catch { }
        }
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
        Close();
        GC.SuppressFinalize(this);
    }
}
