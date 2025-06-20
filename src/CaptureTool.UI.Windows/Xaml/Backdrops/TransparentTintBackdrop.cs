using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.UI.Windows.Xaml.Windows;

public sealed partial class TransparentTintBackdrop : SystemBackdrop
{
    #region Compositor
    internal static Compositor? compositor;
    internal static readonly Lock compositorLock = new();
    internal static Compositor Compositor
    {
        get
        {
            if (compositor == null)
            {
                lock (compositorLock)
                {
                    if (compositor == null)
                    {
                        EnsureDispatcherQueueController();
                        compositor = new Compositor();
                    }
                }
            }
            return compositor;
        }
    }

    private static IntPtr m_dispatcherQueueController = IntPtr.Zero;
    private static void EnsureDispatcherQueueController()
    {
        if (global::Windows.System.DispatcherQueue.GetForCurrentThread() == null && m_dispatcherQueueController == IntPtr.Zero)
        {
            DispatcherQueueOptions options;
            options.dwSize = Marshal.SizeOf<DispatcherQueueOptions>();
            options.threadType = 2;    // DQTYPE_THREAD_CURRENT
            options.apartmentType = 2; // DQTAT_COM_STA

            CreateDispatcherQueueController(options, out m_dispatcherQueueController);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        internal int dwSize;
        internal int threadType;
        internal int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, out IntPtr dispatcherQueueController);
    #endregion

    private HWND _hWnd;
    private HBRUSH _backgroundBrush = HBRUSH.Null;
    private IntPtr _originalWndProc;
    private WndProc? _wndProcDelegate;
    private CompositionColorBrush? _brush;
    private Compositor? _compositor;
    public global::Windows.UI.Color TintColor { get; set; } = Microsoft.UI.Colors.Transparent;

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop connectedTarget, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        _compositor = Compositor;
        _brush = _compositor.CreateColorBrush(TintColor);

        var hwndId = xamlRoot.ContentIslandEnvironment.AppWindowId.Value;
        _hWnd = new HWND((nint)hwndId);

        ConfigureDwm(_hWnd);
        SubclassWindow();
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);
        _brush?.Dispose();
        if (!_backgroundBrush.IsNull)
        {
            PInvoke.DeleteObject(_backgroundBrush);
            _backgroundBrush = HBRUSH.Null;
        }

        RemoveWindowSubclass();
    }

    private static void ConfigureDwm(HWND hwnd)
    {
        // Extends the frame into client area to allow transparency
        PInvoke.DwmExtendFrameIntoClientArea(hwnd, new global::Windows.Win32.UI.Controls.MARGINS());
        PInvoke.DwmEnableBlurBehindWindow(hwnd, new DWM_BLURBEHIND
        {
            dwFlags = 3,
            fEnable = true,
            hRgnBlur = PInvoke.CreateRectRgn(-1, -1, -1, -1)
        });
    }

    private unsafe void SubclassWindow()
    {
        _originalWndProc = PInvoke.GetWindowLongPtr(_hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC);
        _wndProcDelegate = new WndProc(WndProcFunc);
        PInvoke.SetWindowLongPtr(_hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    private unsafe void RemoveWindowSubclass()
    {
        if (_originalWndProc != IntPtr.Zero)
        {
            PInvoke.SetWindowLongPtr(_hWnd, WINDOW_LONG_PTR_INDEX.GWLP_WNDPROC, _originalWndProc);
            _originalWndProc = IntPtr.Zero;
        }
    }

    private unsafe IntPtr WndProcFunc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == 0x0014) // WM_ERASEBKGND
        {
            var hdc = (IntPtr)wParam.Value;
            if (PInvoke.GetClientRect(hwnd, out var rect))
            {
                if (_backgroundBrush.IsNull)
                {
                    _backgroundBrush = PInvoke.CreateSolidBrush(new COLORREF(0)); // fully transparent
                }
                return FillRect(hdc, ref rect, _backgroundBrush);
            }
        }
        else if (msg == 0x031E) // WM_DWMCOMPOSITIONCHANGED
        {
            ConfigureDwm(hwnd);
            return IntPtr.Zero;
        }

        return CallWindowProcW(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int FillRect(IntPtr hDC, [In] ref RECT lprc, HBRUSH hbr);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW", ExactSpelling = true)]
    private static extern IntPtr CallWindowProcW(IntPtr lpPrevWndFunc, HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam);
}
