using System.Runtime.InteropServices;

namespace CaptureTool.Capture.Windows;

internal static partial class NativeMethods
{
    [LibraryImport("CaptureInterop.dll", EntryPoint = "AddNumbers")]
    public static partial int AddNumbers(int a, int b);
}

public sealed partial class ScreenRecorder
{
    public void StartRecording()
    {
        var result = NativeMethods.AddNumbers(3, 4);
        System.Diagnostics.Debug.WriteLine(result);
    }

    public void StopRecording()
    {

    }
}
