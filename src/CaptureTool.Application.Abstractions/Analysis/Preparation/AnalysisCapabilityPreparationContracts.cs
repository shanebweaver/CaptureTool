using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Activity;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Preparation;

public sealed record AnalysisCapabilityPreparationRequest
{
    public AnalysisCapabilityPreparationRequest(
        CapabilityDefinition capability,
        CaptureMediaKind mediaKind,
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("Capability preparation requires a capability.", nameof(capability));
        }

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("Capability preparation requires a purpose.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(processingPolicy);
        if (processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException("The processing policy does not authorize the requested purpose.", nameof(processingPolicy));
        }

        Capability = capability;
        MediaKind = mediaKind;
        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
    }

    public CapabilityDefinition Capability { get; }

    public CaptureMediaKind MediaKind { get; }

    public AnalysisPurpose Purpose { get; }

    public AnalysisProcessingPolicy ProcessingPolicy { get; }
}

public enum AnalysisCapabilityPreparationStatus
{
    Unknown,
    Ready,
    PreparationRequired,
    Preparing,
    Unsupported,
    Disabled,
    Failed,
    Cancelled,
}

public sealed record AnalysisCapabilityPreparationState
{
    private AnalysisCapabilityPreparationState(
        AnalysisCapabilityPreparationStatus status,
        AnalyzerIdentity? analyzer,
        ProcessingBoundary? processingBoundary,
        AnalysisFailure? failure)
    {
        Status = status;
        Analyzer = analyzer;
        ProcessingBoundary = processingBoundary;
        Failure = failure;
    }

    public AnalysisCapabilityPreparationStatus Status { get; }

    public AnalyzerIdentity? Analyzer { get; }

    public ProcessingBoundary? ProcessingBoundary { get; }

    public AnalysisFailure? Failure { get; }

    public static AnalysisCapabilityPreparationState Ready(
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary) =>
        ForAnalyzer(AnalysisCapabilityPreparationStatus.Ready, analyzer, processingBoundary);

    public static AnalysisCapabilityPreparationState PreparationRequired(
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary) =>
        ForAnalyzer(AnalysisCapabilityPreparationStatus.PreparationRequired, analyzer, processingBoundary);

    public static AnalysisCapabilityPreparationState Preparing(
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary) =>
        ForAnalyzer(AnalysisCapabilityPreparationStatus.Preparing, analyzer, processingBoundary);

    public static AnalysisCapabilityPreparationState Unsupported(AnalysisFailure failure) =>
        new(AnalysisCapabilityPreparationStatus.Unsupported, null, null, EnsureTerminal(failure));

    public static AnalysisCapabilityPreparationState Disabled { get; } =
        new(AnalysisCapabilityPreparationStatus.Disabled, null, null, null);

    public static AnalysisCapabilityPreparationState Failed(AnalysisFailure failure)
    {
        if (failure.IsEmpty)
        {
            throw new ArgumentException("Failed preparation requires a bounded failure.", nameof(failure));
        }

        return new(AnalysisCapabilityPreparationStatus.Failed, null, null, failure);
    }

    public static AnalysisCapabilityPreparationState Cancelled { get; } =
        new(AnalysisCapabilityPreparationStatus.Cancelled, null, null, null);

    private static AnalysisCapabilityPreparationState ForAnalyzer(
        AnalysisCapabilityPreparationStatus status,
        AnalyzerIdentity analyzer,
        ProcessingBoundary processingBoundary)
    {
        ArgumentNullException.ThrowIfNull(analyzer);
        if (!Enum.IsDefined(processingBoundary) ||
            processingBoundary == CaptureTool.Domain.Analysis.ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        return new(status, analyzer, processingBoundary, null);
    }

    private static AnalysisFailure EnsureTerminal(AnalysisFailure failure)
    {
        if (failure.Disposition != AnalysisFailureDisposition.Terminal)
        {
            throw new ArgumentException("Unsupported preparation requires a terminal failure.", nameof(failure));
        }

        return failure;
    }
}

public sealed record AnalysisCapabilityPreparationProgress
{
    public AnalysisCapabilityPreparationProgress(double fractionComplete)
    {
        if (!double.IsFinite(fractionComplete) || fractionComplete is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fractionComplete));
        }

        FractionComplete = fractionComplete;
    }

    public double FractionComplete { get; }
}

public enum CaptureAnalyzerPreparationStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    Disabled,
    Failed,
    Cancelled,
}

public sealed record CaptureAnalyzerPreparationResult
{
    private CaptureAnalyzerPreparationResult(
        CaptureAnalyzerPreparationStatus status,
        AnalysisFailure? failure)
    {
        Status = status;
        Failure = failure;
    }

    public CaptureAnalyzerPreparationStatus Status { get; }

    public AnalysisFailure? Failure { get; }

    public static CaptureAnalyzerPreparationResult Succeeded { get; } = new(
        CaptureAnalyzerPreparationStatus.Succeeded,
        null);

    public static CaptureAnalyzerPreparationResult Disabled { get; } = new(
        CaptureAnalyzerPreparationStatus.Disabled,
        null);

    public static CaptureAnalyzerPreparationResult Cancelled { get; } = new(
        CaptureAnalyzerPreparationStatus.Cancelled,
        null);

    public static CaptureAnalyzerPreparationResult Unsupported(AnalysisFailure failure)
    {
        if (failure.Disposition != AnalysisFailureDisposition.Terminal)
        {
            throw new ArgumentException(
                "Unsupported analyzer preparation requires a terminal failure.",
                nameof(failure));
        }

        return new(CaptureAnalyzerPreparationStatus.Unsupported, failure);
    }

    public static CaptureAnalyzerPreparationResult Failed(AnalysisFailure failure)
    {
        if (failure.IsEmpty)
        {
            throw new ArgumentException(
                "Failed analyzer preparation requires a bounded failure.",
                nameof(failure));
        }

        return new(CaptureAnalyzerPreparationStatus.Failed, failure);
    }
}

public interface IPreparableCaptureAnalyzer : ICaptureAnalyzer
{
    Task<CaptureAnalyzerPreparationResult> PrepareAsync(
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IAnalysisCapabilityPreparationQueryService
{
    ValueTask<AnalysisCapabilityPreparationState> GetStateAsync(
        AnalysisCapabilityPreparationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IUserInitiatedAnalysisCapabilityPreparationService
{
    Task<AnalysisCapabilityPreparationState> PrepareAsync(
        AnalysisCapabilityPreparationRequest request,
        IProgress<AnalysisCapabilityPreparationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IAnalysisCapabilityPreparationActivityQueryService
{
    event EventHandler? ActivityChanged;

    IReadOnlyList<CaptureAnalysisModelPreparationActivity> GetCurrentPreparations();
}
