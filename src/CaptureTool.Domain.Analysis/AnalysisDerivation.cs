namespace CaptureTool.Domain.Analysis;

/// <summary>A consulted first-level capability. A null result identity means it was absent.</summary>
public sealed record AnalysisInputReference
{
    public AnalysisCapability Capability { get; }
    public Guid? ResultId { get; }

    public AnalysisInputReference(AnalysisCapability capability, Guid? resultId)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (capability != AnalysisCapability.FileDetails && capability != AnalysisCapability.TextRecognition &&
            capability != AnalysisCapability.QrCodeDetection && capability != AnalysisCapability.Description &&
            capability != AnalysisCapability.Transcription)
            throw new ArgumentException("Derived inputs must be supported first-level capabilities.", nameof(capability));
        if (resultId == Guid.Empty) throw new ArgumentException("Result identity cannot be empty.", nameof(resultId));
        Capability = capability;
        ResultId = resultId;
    }
}

/// <summary>Exact inputs consulted by one processor, scoped to the containing capture revision.</summary>
public sealed class AnalysisDerivation
{
    public IReadOnlyList<AnalysisInputReference> Inputs { get; }

    public AnalysisDerivation(IEnumerable<AnalysisInputReference> inputs)
    {
        Inputs = AnalysisGuard.Freeze(inputs);
        if (!Inputs.Any(input => input.ResultId != null) ||
            Inputs.Select(input => input.Capability).Distinct().Count() != Inputs.Count ||
            Inputs.Where(input => input.ResultId != null).Select(input => input.ResultId).Distinct().Count() !=
                Inputs.Count(input => input.ResultId != null))
            throw new ArgumentException("Specify distinct capabilities and identities with at least one present input.", nameof(inputs));
    }

    public bool Matches(IReadOnlyList<AnalysisResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return Inputs.All(input => results.SingleOrDefault(result => result.Payload.Capability == input.Capability)?.ResultId == input.ResultId);
    }
}
