using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Analyzers;

[Flags]
public enum CaptureAnalyzerDataKind
{
    None = 0,
    SourceMedia = 1 << 0,
    DecodedPixels = 1 << 1,
    ExtractedText = 1 << 2,
    MediaProperties = 1 << 3,
}

[Flags]
public enum CaptureAnalyzerRequirement
{
    None = 0,
    OperatingSystemCapability = 1 << 0,
    HardwareAcceleration = 1 << 1,
    ModelPackage = 1 << 2,
    NetworkConnectivity = 1 << 3,
    UserInitiatedPreparation = 1 << 4,
}

public enum CaptureAnalyzerWorkloadClass
{
    Unknown,
    Lightweight,
    AiIntensive,
}

public sealed record CaptureAnalyzerDescriptor
{
    private readonly IReadOnlyList<CaptureMediaKind> _supportedMediaKinds;

    public CaptureAnalyzerDescriptor(
        CapabilityDefinition capability,
        AnalyzerIdentity identity,
        IEnumerable<CaptureMediaKind> supportedMediaKinds,
        ProcessingBoundary processingBoundary,
        CaptureAnalyzerDataKind dataSent,
        CaptureAnalyzerRequirement requirements,
        CaptureAnalyzerWorkloadClass workloadClass,
        long? maximumSourceBytes,
        int qualityTier)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("An analyzer descriptor requires a capability.", nameof(capability));
        }

        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(supportedMediaKinds);

        CaptureMediaKind[] mediaKinds = [.. supportedMediaKinds.Distinct()];
        if (mediaKinds.Length == 0 || mediaKinds.Any(mediaKind =>
            !Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown))
        {
            throw new ArgumentException(
                "An analyzer descriptor requires at least one known media kind.",
                nameof(supportedMediaKinds));
        }

        if (!Enum.IsDefined(processingBoundary) || processingBoundary == ProcessingBoundary.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(processingBoundary));
        }

        const CaptureAnalyzerDataKind AllDataKinds =
            CaptureAnalyzerDataKind.SourceMedia |
            CaptureAnalyzerDataKind.DecodedPixels |
            CaptureAnalyzerDataKind.ExtractedText |
            CaptureAnalyzerDataKind.MediaProperties;
        if ((dataSent & ~AllDataKinds) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dataSent));
        }

        const CaptureAnalyzerRequirement AllRequirements =
            CaptureAnalyzerRequirement.OperatingSystemCapability |
            CaptureAnalyzerRequirement.HardwareAcceleration |
            CaptureAnalyzerRequirement.ModelPackage |
            CaptureAnalyzerRequirement.NetworkConnectivity |
            CaptureAnalyzerRequirement.UserInitiatedPreparation;
        if ((requirements & ~AllRequirements) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requirements));
        }

        if (!Enum.IsDefined(workloadClass) || workloadClass == CaptureAnalyzerWorkloadClass.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(workloadClass));
        }

        if (maximumSourceBytes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumSourceBytes));
        }

        if (qualityTier < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(qualityTier));
        }

        if (processingBoundary == ProcessingBoundary.OnDevice && dataSent != CaptureAnalyzerDataKind.None)
        {
            throw new ArgumentException(
                "An on-device analyzer cannot declare data sent across a processing boundary.",
                nameof(dataSent));
        }

        if (processingBoundary == ProcessingBoundary.Remote && dataSent == CaptureAnalyzerDataKind.None)
        {
            throw new ArgumentException(
                "A remote analyzer must declare the data sent across its processing boundary.",
                nameof(dataSent));
        }


        if (processingBoundary == ProcessingBoundary.Remote &&
            !requirements.HasFlag(CaptureAnalyzerRequirement.NetworkConnectivity))
        {
            throw new ArgumentException(
                "A remote analyzer must declare a network-connectivity requirement.",
                nameof(requirements));
        }

        Capability = capability;
        Identity = identity;
        _supportedMediaKinds = Array.AsReadOnly(mediaKinds);
        ProcessingBoundary = processingBoundary;
        DataSent = dataSent;
        Requirements = requirements;
        WorkloadClass = workloadClass;
        MaximumSourceBytes = maximumSourceBytes;
        QualityTier = qualityTier;
    }

    public CapabilityDefinition Capability { get; }

    public AnalyzerIdentity Identity { get; }

    public AnalyzerRevision Revision => Identity.Revision;

    public IReadOnlyList<CaptureMediaKind> SupportedMediaKinds => _supportedMediaKinds;

    public ProcessingBoundary ProcessingBoundary { get; }

    public CaptureAnalyzerDataKind DataSent { get; }

    public CaptureAnalyzerRequirement Requirements { get; }

    public CaptureAnalyzerWorkloadClass WorkloadClass { get; }

    public long? MaximumSourceBytes { get; }

    public int QualityTier { get; }
}

public enum CaptureAnalyzerAvailabilityStatus
{
    Unknown,
    Available,
    Disabled,
    Unsupported,
    PreparationRequired,
    TemporarilyUnavailable,
}

public sealed record CaptureAnalyzerAvailability
{
    private CaptureAnalyzerAvailability(
        CaptureAnalyzerAvailabilityStatus status,
        AnalysisFailure? failure)
    {
        Status = status;
        Failure = failure;
    }

    public CaptureAnalyzerAvailabilityStatus Status { get; }

    public AnalysisFailure? Failure { get; }

    public static CaptureAnalyzerAvailability Available { get; } = new(
        CaptureAnalyzerAvailabilityStatus.Available,
        null);

    public static CaptureAnalyzerAvailability Disabled { get; } = new(
        CaptureAnalyzerAvailabilityStatus.Disabled,
        null);

    public static CaptureAnalyzerAvailability PreparationRequired { get; } = new(
        CaptureAnalyzerAvailabilityStatus.PreparationRequired,
        null);

    public static CaptureAnalyzerAvailability Unsupported(AnalysisFailure failure)
    {
        EnsureDisposition(failure, AnalysisFailureDisposition.Terminal);
        return new(CaptureAnalyzerAvailabilityStatus.Unsupported, failure);
    }

    public static CaptureAnalyzerAvailability TemporarilyUnavailable(AnalysisFailure failure)
    {
        EnsureDisposition(failure, AnalysisFailureDisposition.Transient);
        return new(CaptureAnalyzerAvailabilityStatus.TemporarilyUnavailable, failure);
    }

    private static void EnsureDisposition(
        AnalysisFailure failure,
        AnalysisFailureDisposition expectedDisposition)
    {
        if (failure.Disposition != expectedDisposition)
        {
            throw new ArgumentException(
                $"The availability failure must be {expectedDisposition}.",
                nameof(failure));
        }
    }
}

public interface ICaptureAnalysisSource
{
    CaptureId CaptureId { get; }

    CaptureMediaKind MediaKind { get; }

    SourceRevision SourceRevision { get; }

    ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default);
}

public sealed record CaptureAnalyzerAvailabilityRequest
{
    public CaptureAnalyzerAvailabilityRequest(
        CaptureAnalyzerDescriptor descriptor,
        CaptureMediaKind mediaKind,
        long sourceLength,
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (!Enum.IsDefined(mediaKind) || mediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaKind));
        }

        if (sourceLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceLength));
        }

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("An availability request requires a purpose.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(processingPolicy);
        if (processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException(
                "The processing policy does not authorize the requested purpose.",
                nameof(processingPolicy));
        }

        if (!descriptor.SupportedMediaKinds.Contains(mediaKind) ||
            (descriptor.MaximumSourceBytes != null && sourceLength > descriptor.MaximumSourceBytes) ||
            !processingPolicy.IsEligible(descriptor.Identity, descriptor.ProcessingBoundary, purpose))
        {
            throw new ArgumentException(
                "The analyzer is not eligible for this media source and processing policy.",
                nameof(descriptor));
        }

        Descriptor = descriptor;
        MediaKind = mediaKind;
        SourceLength = sourceLength;
        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public CapabilityDefinition Capability => Descriptor.Capability;

    public CaptureMediaKind MediaKind { get; }

    public long SourceLength { get; }

    public AnalysisPurpose Purpose { get; }

    public ProcessingBoundary AuthorizedProcessingBoundary => Descriptor.ProcessingBoundary;

    public AnalysisProcessingPolicy ProcessingPolicy { get; }

    public bool IsEligibleFor(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor == Descriptor;
    }
}

public sealed record CaptureAnalysisRequest
{
    public CaptureAnalysisRequest(
        CaptureAnalyzerDescriptor descriptor,
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy,
        ICaptureAnalysisSource source)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (purpose.IsEmpty)
        {
            throw new ArgumentException("An analysis request requires a purpose.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(processingPolicy);
        ArgumentNullException.ThrowIfNull(source);
        if (processingPolicy.AuthorizedPurpose != purpose)
        {
            throw new ArgumentException(
                "The processing policy does not authorize the requested purpose.",
                nameof(processingPolicy));
        }

        if (source.CaptureId.IsEmpty || source.SourceRevision.IsEmpty ||
            !Enum.IsDefined(source.MediaKind) || source.MediaKind == CaptureMediaKind.Unknown)
        {
            throw new ArgumentException("An analysis source must identify verified capture content.", nameof(source));
        }

        if (!descriptor.SupportedMediaKinds.Contains(source.MediaKind) ||
            (descriptor.MaximumSourceBytes != null &&
             source.SourceRevision.Length > descriptor.MaximumSourceBytes) ||
            !processingPolicy.IsEligible(descriptor.Identity, descriptor.ProcessingBoundary, purpose))
        {
            throw new ArgumentException(
                "The analyzer is not eligible for this verified source and processing policy.",
                nameof(descriptor));
        }

        Descriptor = descriptor;
        Purpose = purpose;
        ProcessingPolicy = processingPolicy;
        Source = source;
    }

    public CaptureAnalyzerDescriptor Descriptor { get; }

    public CapabilityDefinition Capability => Descriptor.Capability;

    public AnalysisPurpose Purpose { get; }

    public ProcessingBoundary AuthorizedProcessingBoundary => Descriptor.ProcessingBoundary;

    public AnalysisProcessingPolicy ProcessingPolicy { get; }

    public ICaptureAnalysisSource Source { get; }

    public bool IsEligibleFor(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return descriptor == Descriptor;
    }
}

public enum CaptureAnalyzerOutputStatus
{
    Unknown,
    Succeeded,
    Unsupported,
    Failed,
    Cancelled,
}

public sealed record CaptureAnalyzerOutput
{
    private CaptureAnalyzerOutput(
        CaptureAnalyzerOutputStatus status,
        CapabilityPayload? payload,
        AnalysisFailure? failure)
    {
        Status = status;
        Payload = payload;
        Failure = failure;
    }

    public CaptureAnalyzerOutputStatus Status { get; }

    public CapabilityPayload? Payload { get; }

    public AnalysisFailure? Failure { get; }

    public CapabilityDefinition? Capability => Payload?.Definition;

    public static CaptureAnalyzerOutput Succeeded(CapabilityPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new(CaptureAnalyzerOutputStatus.Succeeded, payload, null);
    }

    public static CaptureAnalyzerOutput Unsupported(AnalysisFailure failure)
    {
        EnsureDisposition(failure, AnalysisFailureDisposition.Terminal);
        return new(CaptureAnalyzerOutputStatus.Unsupported, null, failure);
    }

    public static CaptureAnalyzerOutput Failed(AnalysisFailure failure)
    {
        if (failure.IsEmpty)
        {
            throw new ArgumentException("A failed analyzer output requires a bounded failure.", nameof(failure));
        }

        return new(CaptureAnalyzerOutputStatus.Failed, null, failure);
    }

    public static CaptureAnalyzerOutput Cancelled { get; } = new(
        CaptureAnalyzerOutputStatus.Cancelled,
        null,
        null);

    public bool IsCompatibleWith(CaptureAnalyzerDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return Status != CaptureAnalyzerOutputStatus.Succeeded || Capability == descriptor.Capability;
    }

    private static void EnsureDisposition(
        AnalysisFailure failure,
        AnalysisFailureDisposition expectedDisposition)
    {
        if (failure.Disposition != expectedDisposition)
        {
            throw new ArgumentException(
                $"The analyzer failure must be {expectedDisposition}.",
                nameof(failure));
        }
    }
}

public interface ICaptureAnalyzer
{
    CaptureAnalyzerDescriptor Descriptor { get; }

    ValueTask<CaptureAnalyzerAvailability> GetAvailabilityAsync(
        CaptureAnalyzerAvailabilityRequest request,
        CancellationToken cancellationToken = default);

    Task<CaptureAnalyzerOutput> AnalyzeAsync(
        CaptureAnalysisRequest request,
        CancellationToken cancellationToken = default);
}
