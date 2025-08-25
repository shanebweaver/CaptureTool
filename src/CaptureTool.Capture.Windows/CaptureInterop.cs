using System.Runtime.InteropServices;

namespace CaptureTool.Capture.Windows;

internal static partial class CaptureInterop
{
    [LibraryImport("CaptureInterop.dll", EntryPoint = "AddNumbers")]
    public static partial int AddNumbers(int a, int b);
}
