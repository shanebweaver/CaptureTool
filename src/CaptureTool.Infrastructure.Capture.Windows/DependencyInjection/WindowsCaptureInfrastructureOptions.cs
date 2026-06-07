using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;

public sealed class WindowsCaptureInfrastructureOptions
{
    public bool UseCaptureV2ScreenRecorder { get; set; } = true;

    public Func<IServiceProvider, IScreenRecorder>? CaptureV2ScreenRecorderFactory { get; set; }
}
