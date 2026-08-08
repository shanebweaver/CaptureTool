using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Capture.Assets;

internal interface ICaptureAssetLifecycleService
{
    CaptureId? TryFinalize(string retainedSourcePath, CaptureFileType mediaType);

    void TrySetPreferredOpenPath(
        CaptureId? captureId,
        string retainedSourcePath,
        string preferredOpenPath);
}
