using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Application.Abstractions.Analysis.Preparation;
using CaptureTool.Application.Abstractions.Capture.Assets;
using CaptureTool.Application.Abstractions.Library.CaptureMemory;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestCaptureMemoryService :
    ICaptureMemoryFeatureAvailability,
    ICaptureAnalysisPolicyService,
    ICaptureAnalysisPolicyCommandService,
    IUserInitiatedAnalysisCapabilityPreparationService,
    ICaptureMemorySearchService,
    ICaptureMemoryResultResolver,
    IOpenCaptureMemoryResultUseCase,
    ICaptureAssetRemovalService
{
    private readonly CaptureId _captureId = CaptureId.New();
    private readonly string _capturePath;
    private readonly string _markerDirectory;
    private CaptureAnalysisPolicy _policy = CaptureAnalysisPolicy.Unknown;
    private long _documentRevision = 1;
    private bool _forgotten;

    public UiTestCaptureMemoryService(UiTestLaunchOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.CaptureMemoryImageFilePath);
        _capturePath = Path.GetFullPath(options.CaptureMemoryImageFilePath);
        _markerDirectory = Path.GetFullPath(options.TemporaryFolderPath ?? Path.GetTempPath());
    }

    public bool IsCaptureMemorySearchEnabled => true;

    public ValueTask<CaptureAnalysisPolicySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateSnapshot());
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> ApplyConsentDecisionAsync(
        CaptureAnalysisConsentResponse response,
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedControlDocumentRevision != _documentRevision ||
            response.Decision != CaptureAnalysisConsentDecision.GrantedForFutureCaptures)
        {
            return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Conflict,
                CreateSnapshot()));
        }

        _policy = _policy.GrantFutureCaptures(
            CaptureAnalysisPolicyDefaults.CreateAuthorizationScope(),
            currentSequence: 0);
        _documentRevision++;
        return Succeeded();
    }

    public ValueTask<CaptureAnalysisPolicyChangeResult> AuthorizeExistingCaptureBackfillAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedControlDocumentRevision != _documentRevision)
        {
            return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
                CaptureAnalysisPolicyChangeStatus.Conflict,
                CreateSnapshot()));
        }

        _policy = new CaptureAnalysisPolicy(
            _policy.ConsentState,
            _policy.PolicyRevision,
            _policy.ControlGeneration,
            _policy.AuthorizationScope,
            _policy.IsFutureCaptureAdmissionEnabled,
            _policy.FutureCaptureSequenceWatermark,
            CaptureAnalysisBackfillState.Completed,
            backfillUpperSequence: 1,
            backfillCheckpoint: 1);
        _documentRevision++;
        return Succeeded();
    }

    public Task<AnalysisCapabilityPreparationState> PrepareAsync(
        AnalysisCapabilityPreparationRequest request,
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new AnalysisCapabilityPreparationProgress(1));
        return Task.FromResult(AnalysisCapabilityPreparationState.Ready(
            new AnalyzerIdentity(
                request.Capability.Id.Value,
                "ui-test-provider",
                "ui-test-model",
                "1",
                "1",
                "ui-test-runtime",
                "1",
                "1",
                null),
            ProcessingBoundary.OnDevice));
    }

    public ValueTask<IReadOnlyList<CaptureMemorySearchResult>> SearchAsync(
        CaptureMemorySearchRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool matches = request.Query.Contains("purple", StringComparison.OrdinalIgnoreCase) ||
            request.Query.Contains("comet", StringComparison.OrdinalIgnoreCase);
        if (_forgotten || !matches)
        {
            return ValueTask.FromResult<IReadOnlyList<CaptureMemorySearchResult>>([]);
        }

        return ValueTask.FromResult<IReadOnlyList<CaptureMemorySearchResult>>([
            new CaptureMemorySearchResult(
                _captureId,
                CaptureMediaKind.Image,
                DateTimeOffset.UtcNow,
                1,
                1,
                new CaptureMemoryMatchEvidence(
                    CaptureMemoryMatchKind.OcrText,
                    "PURPLE COMET project launch checklist",
                    new CaptureMemoryPixelBounds(40, 60, 360, 70, 800, 600)))
        ]);
    }

    public ValueTask<CaptureMemoryResultLocation> ResolveAsync(
        CaptureId captureId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new CaptureMemoryResultLocation(
            captureId,
            _forgotten
                ? CaptureMemoryResultLocationStatus.Forgotten
                : CaptureMemoryResultLocationStatus.Available,
            Path.GetFileName(_capturePath),
            _forgotten ? null : _capturePath));
    }

    public bool CanExecute(OpenCaptureMemoryResultRequest request)
    {
        return request != null && !_forgotten && request.CaptureId == _captureId;
    }

    public Task<UseCaseResponse<OpenCaptureMemoryResultResponse>> ExecuteAsync(
        OpenCaptureMemoryResultRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanExecute(request))
        {
            return Task.FromResult(UseCaseResponse<OpenCaptureMemoryResultResponse>.Success(
                new OpenCaptureMemoryResultResponse(OpenCaptureMemoryResultStatus.Forgotten)));
        }

        Directory.CreateDirectory(_markerDirectory);
        File.WriteAllText(Path.Combine(_markerDirectory, "capture-memory-opened.marker"), _captureId.ToString());
        return Task.FromResult(UseCaseResponse<OpenCaptureMemoryResultResponse>.Success(
            new OpenCaptureMemoryResultResponse(OpenCaptureMemoryResultStatus.Opened)));
    }

    public ValueTask<CaptureAssetRemovalResult> RemoveAsync(
        CaptureAssetRemovalRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _forgotten = true;
        Directory.CreateDirectory(_markerDirectory);
        File.WriteAllText(Path.Combine(_markerDirectory, "capture-memory-forgotten.marker"), request.CaptureId.ToString());
        return ValueTask.FromResult(new CaptureAssetRemovalResult(
            CaptureAssetRemovalStatus.Succeeded,
            request));
    }

    public ValueTask<CaptureAnalysisAdmissionDecision> AuthorizeAdmissionAsync(
        CaptureAnalysisAdmissionRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask<CaptureAnalysisAuthorizationDecision> AuthorizeAsync(
        CaptureAnalysisAuthorizationRequest request,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public ValueTask<CaptureAnalysisPolicyChangeResult> ResumeFutureCaptureAdmissionAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default) => Unavailable();

    public ValueTask<CaptureAnalysisPolicyChangeResult> StopFutureCapturesAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default) => Unavailable();

    public ValueTask<CaptureAnalysisPolicyChangeResult> RevokeAsync(
        long expectedControlDocumentRevision,
        CancellationToken cancellationToken = default) => Unavailable();

    private CaptureAnalysisPolicySnapshot CreateSnapshot()
    {
        var control = new CaptureAnalysisControlSnapshot(
            _documentRevision,
            new CaptureAnalysisControlState(_policy, []));
        return new CaptureAnalysisPolicySnapshot(
            CaptureAnalysisPolicySnapshotStatus.Available,
            _policy.IsProcessingAuthorized
                ? CaptureAnalysisConsentState.Granted
                : CaptureAnalysisConsentState.Unknown,
            control);
    }

    private ValueTask<CaptureAnalysisPolicyChangeResult> Succeeded()
    {
        return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
            CaptureAnalysisPolicyChangeStatus.Succeeded,
            CreateSnapshot()));
    }

    private ValueTask<CaptureAnalysisPolicyChangeResult> Unavailable()
    {
        return ValueTask.FromResult(new CaptureAnalysisPolicyChangeResult(
            CaptureAnalysisPolicyChangeStatus.Unavailable,
            CreateSnapshot()));
    }
}
