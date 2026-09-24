using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Domain.Capture;
using CaptureTool.Application.Abstractions.Analysis.Jobs;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Files;

namespace CaptureTool.Application.Analysis.Queries;

internal sealed class CaptureMetadataViewService : ICaptureMetadataViewService
{
    private readonly ICaptureAnalysisStore _store;
    private readonly ICaptureAssetCatalog _captureAssets;
    private readonly ICaptureAnalysisControlStore? _controlStore;
    private readonly ICaptureAnalysisJobStore? _jobs;
    private readonly IFileSystem? _fileSystem;

    public CaptureMetadataViewService(
        ICaptureAnalysisStore store,
        ICaptureAssetCatalog captureAssets,
        ICaptureAnalysisControlStore? controlStore = null,
        ICaptureAnalysisJobStore? jobs = null,
        IFileSystem? fileSystem = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(captureAssets);
        _store = store;
        _captureAssets = captureAssets;
        _controlStore = controlStore;
        _jobs = jobs;
        _fileSystem = fileSystem;
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

        CaptureAnalysisEnrollment? enrollment = null;
        if (_controlStore != null)
        {
            enrollment = (await _controlStore.GetAsync(cancellationToken).ConfigureAwait(false)).State.Enrollments
                .FirstOrDefault(candidate => candidate.CaptureId == asset.Id);
            if (enrollment?.State is CaptureAnalysisEnrollmentState.Excluded or CaptureAnalysisEnrollmentState.Forgotten)
            {
                return EmptySnapshot(asset.Id, request.MediaKind) with { IsEnrolled = false, IsExcluded = true };
            }
        }

        CaptureAnalysisStoreSnapshot? stored = await _store
            .GetAsync(asset.Id, cancellationToken)
            .ConfigureAwait(false);
        CaptureAnalysisRecord? record = stored?.Record;
        if (record != null && record.MediaKind != request.MediaKind)
        {
            return null;
        }

        var states = new List<CaptureMetadataCapabilityState>();
        var jobs = new List<CaptureAnalysisJobIntent>();
        if (_jobs != null)
        {
            await foreach (CaptureAnalysisJobIntent job in _jobs.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (job.Key.Preconditions.CaptureId == asset.Id &&
                    (record == null || job.Key.SourceRevision.HasSameBytesAs(record.SourceRevision)) &&
                    (enrollment == null || job.Key.Preconditions.EnrollmentGeneration == enrollment.EnrollmentGeneration))
                {
                    jobs.Add(job);
                }
            }
        }
        foreach (var (kind, capability) in CapabilitiesFor(request.MediaKind))
        {
            CapabilityAnalysis? analysis = null;
            record?.TryGetAnalysis(capability.Id, out analysis);
            CaptureAnalysisJobIntent? job = jobs.Where(candidate => candidate.Key.Capability.Id == capability.Id)
                .OrderByDescending(candidate => candidate.EnqueuedAtUtc).FirstOrDefault();
            CaptureMetadataProcessingState state = job?.State switch
            {
                CaptureAnalysisJobState.Running => CaptureMetadataProcessingState.Analyzing,
                CaptureAnalysisJobState.Pending or CaptureAnalysisJobState.RetryScheduled => CaptureMetadataProcessingState.Queued,
                CaptureAnalysisJobState.WaitingForCapability => CaptureMetadataProcessingState.WaitingForModel,
                CaptureAnalysisJobState.TerminalFailure when analysis?.LatestOutcome?.State == CapabilityOutcomeState.Unsupported => CaptureMetadataProcessingState.Unsupported,
                CaptureAnalysisJobState.TerminalFailure => CaptureMetadataProcessingState.Failed,
                _ when analysis?.CanonicalResult != null => CaptureMetadataProcessingState.Ready,
                _ when analysis?.LatestOutcome?.State == CapabilityOutcomeState.Unsupported => CaptureMetadataProcessingState.Unsupported,
                _ when analysis?.LatestOutcome?.State == CapabilityOutcomeState.TerminalFailure => CaptureMetadataProcessingState.Failed,
                _ => CaptureMetadataProcessingState.NotAnalyzed,
            };
            states.Add(new(kind, state));
        }

        bool locationCurrent = true;
        if (record != null && _fileSystem != null && request.PersistentSourcePath is string path)
        {
            // Geometry/timing belong to the analyzed retained source, not an edited/exported copy.
            try
            {
                locationCurrent = string.Equals(path, asset.RetainedSourcePath, StringComparison.OrdinalIgnoreCase) &&
                    _fileSystem.FileExists(path) && record.SourceRevision.Matches(new ProvisionalSourceStamp(
                        _fileSystem.GetFileLength(path), new DateTimeOffset(_fileSystem.GetLastWriteTimeUtc(path), TimeSpan.Zero)));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Cached text remains readable when the source cannot be inspected.
                locationCurrent = false;
            }
        }

        if (record == null)
        {
            return EmptySnapshot(asset.Id, request.MediaKind) with
            {
                IsEnrolled = _controlStore == null || enrollment?.State == CaptureAnalysisEnrollmentState.Enrolled,
                CapabilityStates = states.AsReadOnly(),
            };
        }

        return new CaptureMetadataViewSnapshot(
            record.CaptureId,
            record.MediaKind,
            stored!.DocumentRevision,
            GetPayload<MediaPropertiesV1>(record, AnalysisCapabilities.MediaPropertiesV1),
            GetPayload<OcrDocumentV1>(record, AnalysisCapabilities.OcrDocumentV1),
            GetPayload<ImageDescriptionV1>(record, AnalysisCapabilities.ImageDescriptionV1),
            GetPayload<SpeechTranscriptV1>(record, AnalysisCapabilities.SpeechTranscriptV1),
            GetPayload<VideoOcrTrackV1>(record, AnalysisCapabilities.VideoOcrTrackV1),
            GetPayload<VideoDescriptionTrackV1>(record, AnalysisCapabilities.VideoDescriptionTrackV1))
        {
            SourceRevision = record.SourceRevision,
            Passages = CaptureMetadataPassageReader.Read(record),
            CapabilityStates = states.AsReadOnly(),
            IsEnrolled = _controlStore == null || enrollment?.State == CaptureAnalysisEnrollmentState.Enrolled,
            IsLocationCurrent = locationCurrent,
        };
    }

    private static CaptureMetadataViewSnapshot EmptySnapshot(CaptureTool.Domain.CaptureId id, CaptureMediaKind kind) =>
        new(id, kind, 0, null, null, null, null, null, null) { Passages = [] };

    private static IEnumerable<(CaptureMemoryMatchKind Kind, CapabilityDefinition Capability)> CapabilitiesFor(CaptureMediaKind kind)
    {
        if (kind == CaptureMediaKind.Image)
        {
            yield return (CaptureMemoryMatchKind.OcrText, AnalysisCapabilities.OcrDocumentV1);
            yield return (CaptureMemoryMatchKind.ImageDescription, AnalysisCapabilities.ImageDescriptionV1);
        }
        else
        {
            yield return (CaptureMemoryMatchKind.SpeechTranscript, AnalysisCapabilities.SpeechTranscriptV1);
            if (kind == CaptureMediaKind.Video)
            {
                yield return (CaptureMemoryMatchKind.VideoOcrText, AnalysisCapabilities.VideoOcrTrackV1);
                yield return (CaptureMemoryMatchKind.VideoDescription, AnalysisCapabilities.VideoDescriptionTrackV1);
            }
        }
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
