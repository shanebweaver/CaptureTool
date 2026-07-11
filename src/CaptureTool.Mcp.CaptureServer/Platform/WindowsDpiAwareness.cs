using System.Runtime.InteropServices;

namespace CaptureTool.Mcp.CaptureServer.Platform;

internal static class WindowsDpiAwareness
{
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    public static void EnablePerMonitorV2()
    {
        _ = SetProcessDpiAwarenessContext(PerMonitorAwareV2);
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);
}
