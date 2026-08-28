using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Activity;

public sealed record CaptureAnalysisModelPreparationActivity
{
    public CaptureAnalysisModelPreparationActivity(
        AnalyzerIdentity analyzer,
        CapabilityDefinition capability,
        CaptureMediaKind mediaKind,
        double fractionComplete)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException(
                "Model preparation activity requires a capability.",
                nameof(capability));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (!double.IsFinite(fractionComplete) || fractionComplete is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fractionComplete));
        }

        Analyzer = analyzer;
        Capability = capability;
        MediaKind = mediaKind;
        FractionComplete = fractionComplete;
    }

    public AnalyzerIdentity Analyzer { get; }

    public CapabilityDefinition Capability { get; }

    public CaptureMediaKind MediaKind { get; }

    public double FractionComplete { get; }
}

public sealed record CaptureAnalysisActivitySnapshot
{
    private readonly IReadOnlyList<CaptureAnalysisModelPreparationActivity>
        _modelPreparations;

    public CaptureAnalysisActivitySnapshot(
        IEnumerable<CaptureAnalysisModelPreparationActivity>? modelPreparations = null,
        int runningCaptureCount = 0,
        int queuedCaptureCount = 0,
        int waitingCaptureCount = 0,
        int retryCaptureCount = 0,
        int failedCaptureCount = 0,
        bool isBackfillInProgress = false,
        double backfillFractionComplete = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(runningCaptureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(queuedCaptureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(waitingCaptureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(retryCaptureCount);
        ArgumentOutOfRangeException.ThrowIfNegative(failedCaptureCount);
        if (!double.IsFinite(backfillFractionComplete) ||
            backfillFractionComplete is < 0 or > 1 ||
            !isBackfillInProgress && backfillFractionComplete != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backfillFractionComplete));
        }

        CaptureAnalysisModelPreparationActivity[] preparations =
            [.. modelPreparations ?? []];
        if (preparations.Any(activity => activity == null))
        {
            throw new ArgumentException(
                "Model preparation activities cannot contain null values.",
                nameof(modelPreparations));
        }

        _modelPreparations = Array.AsReadOnly(preparations);
        RunningCaptureCount = runningCaptureCount;
        QueuedCaptureCount = queuedCaptureCount;
        WaitingCaptureCount = waitingCaptureCount;
        RetryCaptureCount = retryCaptureCount;
        FailedCaptureCount = failedCaptureCount;
        IsBackfillInProgress = isBackfillInProgress;
        BackfillFractionComplete = backfillFractionComplete;
    }

    public IReadOnlyList<CaptureAnalysisModelPreparationActivity> ModelPreparations =>
        _modelPreparations;

    public int RunningCaptureCount { get; }

    public int QueuedCaptureCount { get; }

    public int WaitingCaptureCount { get; }

    public int RetryCaptureCount { get; }

    public int FailedCaptureCount { get; }

    public bool IsBackfillInProgress { get; }

    public double BackfillFractionComplete { get; }

    public bool HasActivity =>
        ModelPreparations.Count > 0 ||
        IsBackfillInProgress ||
        RunningCaptureCount > 0 ||
        QueuedCaptureCount > 0 ||
        WaitingCaptureCount > 0 ||
        RetryCaptureCount > 0 ||
        FailedCaptureCount > 0;
}

public interface ICaptureAnalysisActivityQueryService
{
    ValueTask<CaptureAnalysisActivitySnapshot> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}
