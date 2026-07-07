using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Features.AudioCapture;

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
        return $"Capture_{timestamp:yyyy-MM-dd}_{timestamp:FFFFF}.wav";
    }
}
