using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace CaptureTool.Capture.Windows;

internal static class DwmSafe
{
    public static bool TryGetExtendedFrameBounds(HWND hwnd, out RECT rect)
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

