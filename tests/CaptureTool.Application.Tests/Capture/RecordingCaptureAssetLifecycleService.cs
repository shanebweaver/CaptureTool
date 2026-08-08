using CaptureTool.Application.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Tests.Capture;

internal sealed class RecordingCaptureAssetLifecycleService : ICaptureAssetLifecycleService
{
    public CaptureId? FinalizedCaptureId { get; set; }

    public Exception? FinalizationException { get; set; }

    public Action? Finalizing { get; set; }

    public List<(string RetainedSourcePath, CaptureFileType MediaType)> Finalizations { get; } = [];

    public List<(CaptureId? CaptureId, string RetainedSourcePath, string PreferredOpenPath)>
        PreferredOpenPathChanges { get; } = [];

    public CaptureId? TryFinalize(string retainedSourcePath, CaptureFileType mediaType)
    {
        Finalizations.Add((retainedSourcePath, mediaType));
        Finalizing?.Invoke();

        if (FinalizationException is not null)
        {
            throw FinalizationException;
        }

        return FinalizedCaptureId;
    }

    public void TrySetPreferredOpenPath(
        CaptureId? captureId,
        string retainedSourcePath,
        string preferredOpenPath)
    {
        PreferredOpenPathChanges.Add((captureId, retainedSourcePath, preferredOpenPath));
    }
}
