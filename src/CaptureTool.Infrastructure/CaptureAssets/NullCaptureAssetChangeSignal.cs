using CaptureTool.Application.Abstractions.Capture.Assets;

namespace CaptureTool.Infrastructure.CaptureAssets;

internal sealed class NullCaptureAssetChangeSignal : ICaptureAssetChangeSignal
{
    public bool TrySignal()
    {
        return false;
    }
}
