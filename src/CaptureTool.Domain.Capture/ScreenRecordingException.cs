namespace CaptureTool.Domain.Capture;

public sealed class ScreenRecordingException : Exception
{
    public ScreenRecordingResult Result { get; }

    public ScreenRecordingException(ScreenRecordingResult result)
        : base($"Screen recording failed during {result.Stage} with HRESULT 0x{result.HResult:X8}.")
    {
        Result = result;
        HResult = result.HResult;
    }
}
