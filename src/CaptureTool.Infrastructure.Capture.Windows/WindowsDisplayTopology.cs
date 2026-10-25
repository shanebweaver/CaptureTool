using CaptureTool.Application.Abstractions.Capture;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows;

public sealed partial class WindowsDisplayTopology : IDisplayTopology
{
    public IReadOnlyList<Rectangle> GetMonitorBounds()
    {
        List<Rectangle> monitors = [];
        Exception? callbackError = null;
        MonitorEnumProc callback = (nint monitor, nint deviceContext, ref NativeRect bounds, nint data) =>
        {
            try
            {
                monitors.Add(Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
                return true;
            }
            catch (Exception ex)
            {
                // Managed exceptions must not escape a reverse P/Invoke callback.
                callbackError = ex;
                return false;
            }
        };

        bool succeeded = EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        if (!succeeded || callbackError is not null)
        {
            throw new InvalidOperationException(
                "Display topology could not be enumerated. Check the desktop session and retry.", callbackError);
        }

        return monitors;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MonitorEnumProc(nint monitor, nint deviceContext, ref NativeRect bounds, nint data);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(nint deviceContext, nint clip, MonitorEnumProc callback, nint data);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
