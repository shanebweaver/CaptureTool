namespace CaptureTool.Domain.Analysis.Payloads;

/// <summary>Application-owned output. Each new kind requires an explicit versioned persistence mapping.</summary>
public abstract class AnalysisPayload
{
    public abstract AnalysisCapability Capability { get; }
    public abstract bool Supports(AnalysisMediaKind mediaKind);
}
