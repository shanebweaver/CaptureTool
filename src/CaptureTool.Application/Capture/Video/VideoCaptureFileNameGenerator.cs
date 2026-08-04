using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Capture.Video;

internal sealed class VideoCaptureFileNameGenerator
{
    private readonly IClock _clock;

    public VideoCaptureFileNameGenerator(IClock clock)
    {
        _clock = clock;
    }

    public string GetNewCaptureFileName()
    {
        DateTime timestamp = _clock.Now;
        return CaptureFileNameFormatter.Create(timestamp, "mp4");
    }
}
