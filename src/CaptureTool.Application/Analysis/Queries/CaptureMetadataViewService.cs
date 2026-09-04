using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Analysis.Queries;

internal sealed class CaptureMetadataViewService : ICaptureMetadataViewService
{
    private readonly ICaptureAnalysisStore _store;
    private readonly ICaptureAssetCatalog _captureAssets;

    public CaptureMetadataViewService(
        ICaptureAnalysisStore store,
        ICaptureAssetCatalog captureAssets)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(captureAssets);
        _store = store;
        _captureAssets = captureAssets;
    }

    public async ValueTask<CaptureMetadataViewSnapshot?> GetAsync(
        CaptureMetadataViewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CaptureAsset? asset = request.CaptureId is CaptureId captureId
            ? _captureAssets.Get(captureId)
            : _captureAssets.FindByPath(request.PersistentSourcePath!);
        if (asset is not { LifecycleState: CaptureAssetLifecycleState.Active } ||
            MapMediaKind(asset.MediaType) != request.MediaKind)
        {
            return null;
        }

        CaptureAnalysisStoreSnapshot? stored = await _store
            .GetAsync(asset.Id, cancellationToken)
            .ConfigureAwait(false);
        if (stored?.Record is not CaptureAnalysisRecord record ||
            record.MediaKind != request.MediaKind)
        {
            return null;
        }

        return new CaptureMetadataViewSnapshot(
            record.CaptureId,
            record.MediaKind,
            stored.DocumentRevision,
            GetPayload<MediaPropertiesV1>(record, AnalysisCapabilities.MediaPropertiesV1),
            GetPayload<OcrDocumentV1>(record, AnalysisCapabilities.OcrDocumentV1),
            GetPayload<ImageDescriptionV1>(record, AnalysisCapabilities.ImageDescriptionV1),
            GetPayload<SpeechTranscriptV1>(record, AnalysisCapabilities.SpeechTranscriptV1),
            GetPayload<VideoOcrTrackV1>(record, AnalysisCapabilities.VideoOcrTrackV1),
            GetPayload<VideoDescriptionTrackV1>(record, AnalysisCapabilities.VideoDescriptionTrackV1));
    }

    private static TPayload? GetPayload<TPayload>(
        CaptureAnalysisRecord record,
        CapabilityDefinition capability)
        where TPayload : CapabilityPayload
    {
        return record.TryGetAnalysis(capability.Id, out CapabilityAnalysis? analysis) &&
            analysis?.Capability == capability
                ? analysis.CanonicalResult?.Payload as TPayload
                : null;
    }

    private static CaptureMediaKind MapMediaKind(CaptureFileType fileType)
    {
        return fileType switch
        {
            CaptureFileType.Image => CaptureMediaKind.Image,
            CaptureFileType.Audio => CaptureMediaKind.Audio,
            CaptureFileType.Video => CaptureMediaKind.Video,
            _ => CaptureMediaKind.Unknown,
        };
    }
}
