using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace CaptureTool.Capture.Windows;

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

                        PInvoke.GetWindowRect(hWnd, out RECT rect);
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
}
