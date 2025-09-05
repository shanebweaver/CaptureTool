using CaptureTool.Capture;
using CaptureTool.UI.Windows.Xaml.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.Win32.SafeHandles;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace CaptureTool.UI.Windows.Xaml.Windows;

internal sealed partial class CaptureOverlayHost : IDisposable
{
    internal sealed partial class DestroyIconSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public DestroyIconSafeHandle(HINSTANCE hINSTANCE) : base(true) {
            handle = hINSTANCE;
        }

        protected override bool ReleaseHandle()
        {
            return PInvoke.DestroyIcon(new(handle));
        }
    }

    private HWND? _hwnd;

    public void Show(MonitorCaptureResult monitor, Rectangle area)
    {
        unsafe
        {
            const string className = "CaptureOverlayWindow";

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

            // Register the window class
            PInvoke.RegisterClassEx(in wndClass);

            HWND hwnd = PInvoke.CreateWindowEx(
                WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TOPMOST,
                className,
                "Capture Overlay Window", // TODO: Replace with a localized string
                WINDOW_STYLE.WS_VISIBLE | WINDOW_STYLE.WS_POPUP,
                24, // x
                24, // y
                400, // w
                66, //h
                new(IntPtr.Zero),
                null,
                new DestroyIconSafeHandle(wndClass.hInstance),
                null);

            // Enable per-pixel alpha transparency
            PInvoke.SetLayeredWindowAttributes(
                hwnd,
                new COLORREF(0),
                128, //255,
                0); // 0 = use per-pixel alpha

            DesktopWindowXamlSource xamlSource = new();
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            xamlSource.Initialize(windowId);

            xamlSource.Content = new CaptureOverlayView();

            _hwnd = hwnd;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(System.Runtime.CompilerServices.CallConvStdcall) })]
    private static LRESULT WindowProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != null)
        {
            PInvoke.DestroyWindow(_hwnd.Value);
        }
    }
}
