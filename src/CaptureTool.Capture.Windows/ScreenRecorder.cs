namespace CaptureTool.Capture.Windows;

public sealed partial class ScreenRecorder
{
    public void StartRecording()
    {
        var result = CaptureInterop.AddNumbers(3, 4);
        System.Diagnostics.Debug.WriteLine(result);
    }

    public void StopRecording()
    {

    }
}
