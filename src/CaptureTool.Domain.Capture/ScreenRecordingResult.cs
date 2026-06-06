namespace CaptureTool.Domain.Capture;

public readonly record struct ScreenRecordingResult(int HResult, ScreenRecordingStage Stage)
{
    public bool Succeeded => HResult >= 0;

    public static ScreenRecordingResult Success => new(0, ScreenRecordingStage.None);
}
