namespace CaptureTool.Domain.Capture.V2;

public readonly record struct CaptureSourceId(uint Value)
{
    public bool IsValid => Value != 0;
}
