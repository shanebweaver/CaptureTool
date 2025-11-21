using CaptureTool.Domains.Capture.Interfaces;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace CaptureTool.Domains.Capture.Implementations.Windows;

public static partial class WindowInfoHelper
{
    public static List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();

        PInvoke.EnumWindows((hWnd, lParam) =>
        {
            if (PInvoke.IsWindowVisible(hWnd))
            {
                int length = PInvoke.GetWindowTextLength(hWnd);
                if (length > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((length + 1) * sizeof(char));

                    try
                    {
                        _ = PInvoke.GetWindowText(hWnd, new(buffer), length + 1);
                        string title = Marshal.PtrToStringUni(buffer)!;

                        if (title == "Windows Input Experience" || title == "Settings")
                        {
                            return false;
                        }

                        if (!TryGetExtendedFrameBounds(hWnd, out RECT rect))
                        {
                            // fallback to normal rect if DWM call fails
                            PInvoke.GetWindowRect(hWnd, out rect);
                        }

                        windows.Add(new WindowInfo(hWnd, title, rect));
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static bool TryGetExtendedFrameBounds(HWND hwnd, out RECT rect)
    {
        rect = default;

        unsafe
        {
            RECT tmp;
            var hr = PInvoke.DwmGetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                &tmp,
                (uint)sizeof(RECT));

            if (hr.Failed)
            {
                return false;
            }

            rect = tmp;
            return true;
        }
    }
}