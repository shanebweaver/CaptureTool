using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCaptureFileNameGenerator
{
    private readonly IClock _clock;

    public AudioCaptureFileNameGenerator(IClock clock)
    {
        _clock = clock;
    }

    public string GetNewCaptureFileName()
    {
        DateTime timestamp = _clock.Now;
        return CaptureFileNameFormatter.Create(timestamp, "wav");
    }
}
