namespace CaptureTool.Domain.Capture;

public enum ScreenRecordingStage
{
    None = 0,
    VideoSourceStop = 1,
    AudioSourceStop = 2,
    SinkFinalize = 3,
    NativeException = 4
}
