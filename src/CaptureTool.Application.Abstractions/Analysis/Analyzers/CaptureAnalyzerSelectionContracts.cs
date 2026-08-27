using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Analyzers;

public enum CaptureAnalyzerSelectionMode
{
    Unknown,
    Automatic,
    Prefer,
    Force,
    Off,
}

public sealed record CaptureAnalyzerSelectionTarget
{
    public CaptureAnalyzerSelectionTarget(string providerId, string analyzerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(analyzerId);
        ProviderId = providerId;
        AnalyzerId = analyzerId;
    }

    public string ProviderId { get; }

    public string AnalyzerId { get; }
}

public sealed record CaptureAnalyzerSelection
{
    public CaptureAnalyzerSelection(
        CapabilityDefinition capability,
        CaptureAnalyzerSelectionMode mode,
        CaptureAnalyzerSelectionTarget? target = null)
    {
        if (capability.Id.IsEmpty)
        {
            throw new ArgumentException("An analyzer selection requires a capability.", nameof(capability));
        }

        if (!Enum.IsDefined(mode) || mode == CaptureAnalyzerSelectionMode.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        bool requiresTarget = mode is CaptureAnalyzerSelectionMode.Prefer or
            CaptureAnalyzerSelectionMode.Force;
        if (requiresTarget != (target != null))
        {
            throw new ArgumentException(
                requiresTarget
                    ? "Preferred and forced selections require an analyzer target."
                    : "Automatic and off selections cannot specify an analyzer target.",
                nameof(target));
        }

        Capability = capability;
        Mode = mode;
        Target = target;
    }

    public CapabilityDefinition Capability { get; }

    public CaptureAnalyzerSelectionMode Mode { get; }

    public CaptureAnalyzerSelectionTarget? Target { get; }

    public static CaptureAnalyzerSelection Automatic(CapabilityDefinition capability) =>
        new(capability, CaptureAnalyzerSelectionMode.Automatic);
}

public enum CaptureAnalyzerSelectionSaveStatus
{
    Unknown,
    Saved,
    Unchanged,
    InvalidSelection,
    PersistenceFailed,
    Unavailable,
}

public readonly record struct CaptureAnalyzerSelectionSaveResult(
    CaptureAnalyzerSelectionSaveStatus Status)
{
    public bool Succeeded => Status is CaptureAnalyzerSelectionSaveStatus.Saved or
        CaptureAnalyzerSelectionSaveStatus.Unchanged;
}

public interface ICaptureAnalyzerSelectionService
{
    long Revision { get; }

    CaptureAnalyzerSelection GetSelection(CapabilityDefinition capability);

    int GetPreference(CaptureAnalyzerDescriptor descriptor);

    bool IsAllowed(CaptureAnalyzerDescriptor descriptor);

    bool? GetFeatureEnabledOverride(AnalyzerIdentity analyzer);

    ValueTask<CaptureAnalyzerSelectionSaveResult> SaveAsync(
        IEnumerable<CaptureAnalyzerSelection> selections,
        CancellationToken cancellationToken = default);
}
