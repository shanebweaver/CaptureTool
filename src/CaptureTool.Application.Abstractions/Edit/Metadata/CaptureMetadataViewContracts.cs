using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Abstractions.Edit.Metadata;

public sealed record CaptureMetadataViewRequest
{
    public CaptureMetadataViewRequest(
        CaptureMediaKind mediaKind,
        CaptureId? captureId = null,
        string? persistentSourcePath = null)
    {
        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (captureId is { IsEmpty: true })
        {
            throw new ArgumentException("A supplied capture ID cannot be empty.", nameof(captureId));
        }

        if (captureId == null && string.IsNullOrWhiteSpace(persistentSourcePath))
        {
            throw new ArgumentException(
                "Metadata lookup requires a capture ID or persistent source path.",
                nameof(persistentSourcePath));
        }

        MediaKind = mediaKind;
        CaptureId = captureId;
        PersistentSourcePath = string.IsNullOrWhiteSpace(persistentSourcePath)
            ? null
            : Path.GetFullPath(persistentSourcePath);
    }

    public CaptureMediaKind MediaKind { get; }

    public CaptureId? CaptureId { get; }

    public string? PersistentSourcePath { get; }
}

public sealed record CaptureMetadataViewSnapshot(
    CaptureId CaptureId,
    CaptureMediaKind MediaKind,
    long DocumentRevision,
    MediaPropertiesV1? MediaProperties,
    OcrDocumentV1? ImageText,
    ImageDescriptionV1? ImageDescription,
    SpeechTranscriptV1? SpeechTranscript,
    VideoOcrTrackV1? VideoText,
    VideoDescriptionTrackV1? VideoDescription);

public interface ICaptureMetadataViewService
{
    ValueTask<CaptureMetadataViewSnapshot?> GetAsync(
        CaptureMetadataViewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CaptureAnalysisChangedEventArgs : EventArgs
{
    public CaptureAnalysisChangedEventArgs(CaptureId captureId, bool wasDeleted)
    {
        if (captureId.IsEmpty)
        {
            throw new ArgumentException("An analysis change requires a capture ID.", nameof(captureId));
        }

        CaptureId = captureId;
        WasDeleted = wasDeleted;
    }

    public CaptureId CaptureId { get; }

    public bool WasDeleted { get; }
}

public interface ICaptureAnalysisChangeNotifier
{
    event EventHandler<CaptureAnalysisChangedEventArgs>? AnalysisChanged;
}
