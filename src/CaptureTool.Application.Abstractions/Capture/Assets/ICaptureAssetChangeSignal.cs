namespace CaptureTool.Application.Abstractions.Capture.Assets;

public interface ICaptureAssetChangeSignal
{
    bool TrySignal();
}
