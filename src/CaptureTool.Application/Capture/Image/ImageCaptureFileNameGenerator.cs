using CaptureTool.Application.Abstractions.Time;

namespace CaptureTool.Application.Capture.Image;

internal sealed class ImageCaptureFileNameGenerator
{
    private readonly IClock _clock;

    public ImageCaptureFileNameGenerator(IClock clock)
    {
        _clock = clock;
    }

    public string GetNewCaptureFileName()
    {
        DateTime timestamp = _clock.Now;
        return $"Capture_{timestamp:yyyy-MM-dd}_{timestamp:FFFFF}.png";
    }
}
