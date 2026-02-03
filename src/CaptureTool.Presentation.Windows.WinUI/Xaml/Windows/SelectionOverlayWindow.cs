using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Application.Interfaces.ViewModels.Options;
using CaptureTool.Presentation.Windows.WinUI.Utils;
using CaptureTool.Presentation.Windows.WinUI.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Hosting;
using System.Collections.Concurrent;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.Presentation.Windows.WinUI.Xaml.Windows;

public sealed partial class SelectionOverlayWindow : IDisposable
{
    [DllImport("user32.dll")]
    private static extern int FillRect(nint hDC, in RECT lprc, nint hbr);

    private static readonly ConcurrentDictionary<nint, SelectionOverlayWindow> _windowInstances = new();

    private readonly HWND _hwnd;
    private readonly Rectangle _monitorBounds;
    private readonly bool _isPrimary;
    private readonly SelectionOverlayWindowOptions _overlayOptions;
    private DesktopWindowXamlSource? _xamlSource;
    private SelectionOverlayWindowView? _view;
    private int _windowShownFlag = 0;
    private int _isClosed = 0; // Using int for Interlocked operations
    private bool _disposed = false;
    private HBITMAP _backgroundBitmap;
    private HBRUSH _backgroundBrush;

    public ISelectionOverlayWindowViewModel? ViewModel => _view?.ViewModel;
    public Rectangle MonitorBounds => _monitorBounds;
    public bool IsClosed => _isClosed == 1;

    public SelectionOverlayWindow(SelectionOverlayWindowOptions overlayOptions)
    {
        _overlayOptions = overlayOptions;
        _monitorBounds = overlayOptions.Monitor.MonitorBounds;
        _isPrimary = overlayOptions.Monitor.IsPrimary;

        // Create the Win32 window initially hidden with background image
        _hwnd = CreateWindow();

        // Register this instance for WindowProc callbacks
        unsafe
        {
            _windowInstances[(nint)_hwnd.Value] = this;
        }

        // Initialize XAML content
        InitializeXamlContent(overlayOptions);
    }

    private unsafe HWND CreateWindow()
    {
        const string className = "SelectionOverlayWindow";

        // Create bitmap and brush from monitor pixel buffer
        CreateBackgroundBrushFromMonitor();

        // Register window class with our background brush
        // Note: Multiple windows share the same class but handle backgrounds individually via WM_ERASEBKGND
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
            hbrBackground = HBRUSH.Null, // Background is per-instance via WM_ERASEBKGND
            lpszMenuName = null,
            hIconSm = HICON.Null
        };
        fixed (char* name = className)
        {
            wndClass.lpszClassName = name;
            // Try to register; silently ignore ERROR_CLASS_ALREADY_EXISTS (0x582)
            var atom = PInvoke.RegisterClassEx(in wndClass);
            if (atom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0x582) // ERROR_CLASS_ALREADY_EXISTS
                {
                    throw new InvalidOperationException($"Failed to register window class. Error: 0x{error:X}");
                }
            }
        }

        // Create window WITHOUT WS_VISIBLE flag - window starts hidden
        // Use CW_USEDEFAULT temporarily for creation, we'll position it properly before showing
        HWND hwnd = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_TOPMOST | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            className,
            null,
            WINDOW_STYLE.WS_POPUP, // No WS_VISIBLE - starts hidden
            0, 0, 0, 0, // Position and size will be set by SetWindowPos
            new(IntPtr.Zero),
            null,
            null,
            null);

        // Apply borderless styles without showing
        ApplyBorderlessStyles(hwnd);

        // Set final position and size in one atomic operation while still hidden
        PInvoke.SetWindowPos(
            hwnd,
            new HWND(-1), // HWND_TOPMOST
            _monitorBounds.X,
            _monitorBounds.Y,
            _monitorBounds.Width,
            _monitorBounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);

        if (_isPrimary)
        {
            Win32WindowHelpers.ExcludeFromScreenCapture(hwnd);
        }

        return hwnd;
    }

    private unsafe void CreateBackgroundBrushFromMonitor()
    {
        var monitor = _overlayOptions.Monitor;
        var bounds = monitor.MonitorBounds;
        var pixelBuffer = monitor.PixelBuffer;

        // Get device context for creating the bitmap
        var hwndDesktop = new HWND(IntPtr.Zero);
        var hdc = PInvoke.GetDC(hwndDesktop);

        try
        {
            // Create bitmap
            _backgroundBitmap = PInvoke.CreateCompatibleBitmap(hdc, bounds.Width, bounds.Height);

            if (!_backgroundBitmap.IsNull)
            {
                // Prepare BITMAPINFO structure
                int headerSize = sizeof(BITMAPINFOHEADER);
                int bmiSize = headerSize;
                byte[] bmiBytes = new byte[bmiSize];

                fixed (byte* pBmi = bmiBytes)
                {
                    BITMAPINFOHEADER* bmiHeader = (BITMAPINFOHEADER*)pBmi;
                    bmiHeader->biSize = (uint)headerSize;
                    bmiHeader->biWidth = bounds.Width;
                    bmiHeader->biHeight = -bounds.Height; // Negative for top-down
                    bmiHeader->biPlanes = 1;
                    bmiHeader->biBitCount = 32;
                    bmiHeader->biCompression = 0; // BI_RGB

                    // Set the bitmap bits
                    fixed (byte* pSrc = pixelBuffer)
                    {
                        _ = PInvoke.SetDIBits(
                            hdc,
                            _backgroundBitmap,
                            0,
                            (uint)bounds.Height,
                            pSrc,
                            (BITMAPINFO*)pBmi,
                            DIB_USAGE.DIB_RGB_COLORS);
                    }
                }

                // Create pattern brush from bitmap
                _backgroundBrush = PInvoke.CreatePatternBrush(_backgroundBitmap);

                if (_backgroundBrush.IsNull)
                {
                    // Failed to create brush, clean up bitmap and use fallback
                    PInvoke.DeleteObject(new HGDIOBJ(_backgroundBitmap.Value));
                    _backgroundBitmap = default;
                }
            }

            // Fallback: If bitmap or brush creation failed, create a solid black brush
            if (_backgroundBrush.IsNull)
            {
                _backgroundBrush = PInvoke.CreateSolidBrush(new COLORREF(0)); // Black
            }
        }
        finally
        {
            // Cleanup DC
            _ = PInvoke.ReleaseDC(hwndDesktop, hdc);
        }
    }

    private static void ApplyBorderlessStyles(HWND hwnd)
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
            SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
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

        ViewModel?.Load(overlayOptions);
    }

    public void Activate()
    {
        if (IsClosed)
        {
            return;
        }

        // Use Interlocked for thread-safe check-and-set
        if (Interlocked.CompareExchange(ref _windowShownFlag, 1, 0) == 0)
        {
            // Show window using SetWindowPos for atomic operation - no jitter
            PInvoke.SetWindowPos(
                _hwnd,
                new HWND(-1), // HWND_TOPMOST
                0, 0, 0, 0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
                SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
                SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }
    }

    public void FocusContent()
    {
        if (IsClosed || _xamlSource == null || _view == null)
        {
            return;
        }

        try
        {
            // Navigate focus into the XAML island
            var request = new XamlSourceFocusNavigationRequest(
                XamlSourceFocusNavigationReason.Programmatic);
            _xamlSource.NavigateFocus(request);

            // Set focus to the root panel to ensure keyboard input is captured
            _view.FocusRootPanel();
        }
        catch { }
    }

    public nint GetWindowHandle()
    {
        if (IsClosed || _disposed)
        {
            return IntPtr.Zero;
        }
        return _hwnd;
    }

    public void Close()
    {
        // Use Interlocked for thread-safe check-and-set
        if (Interlocked.CompareExchange(ref _isClosed, 1, 0) != 0)
        {
            return; // Already closed
        }

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

        unsafe
        {
            // Clean up GDI resources before destroying window
            if (!_backgroundBrush.IsNull)
            {
                PInvoke.DeleteObject(new HGDIOBJ(_backgroundBrush.Value));
                _backgroundBrush = default;
            }

            if (!_backgroundBitmap.IsNull)
            {
                PInvoke.DeleteObject(new HGDIOBJ(_backgroundBitmap.Value));
                _backgroundBitmap = default;
            }

            // Now destroy the window
            if ((nint)_hwnd.Value != IntPtr.Zero)
            {
                // Unregister this instance
                _windowInstances.TryRemove((nint)_hwnd.Value, out _);

                try
                {
                    PInvoke.DestroyWindow(_hwnd);
                }
                catch { }
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static unsafe LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        const uint WM_ERASEBKGND = 0x0014;
        const uint WM_ACTIVATE = 0x0006;

        // Handle window activation - when user clicks on any window
        if (msg == WM_ACTIVATE)
        {
            nint hwndPtr = (nint)hwnd.Value;
            if (_windowInstances.TryGetValue(hwndPtr, out var window))
            {
                // wParam low word contains activation state
                // WA_ACTIVE (1) or WA_CLICKACTIVE (2) means being activated
                int activationState = (int)wParam.Value & 0xFFFF;
                if (activationState == 1 || activationState == 2)
                {
                    // Ensure this window is foreground
                    Win32WindowHelpers.SetForegroundWindow(hwnd);

                    // Focus the XAML content
                    window.FocusContent();
                }
            }
        }

        // Handle background painting for this specific window instance
        if (msg == WM_ERASEBKGND)
        {
            nint hwndPtr = (nint)hwnd.Value;
            if (_windowInstances.TryGetValue(hwndPtr, out var window))
            {
                if (!window._backgroundBrush.IsNull)
                {
                    // wParam contains the HDC
                    nint hdcValue = (nint)wParam.Value;
                    PInvoke.GetClientRect(hwnd, out RECT rect);

                    // Fill with our background brush using direct user32 FillRect
                    unsafe
                    {
                        _ = FillRect(hdcValue, in rect, (nint)window._backgroundBrush.Value);
                    }
                    return new LRESULT(1); // Return non-zero to indicate we handled it
                }
            }
        }

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
