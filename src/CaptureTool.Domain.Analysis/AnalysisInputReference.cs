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
        {
            throw new ArgumentException("Derived inputs must be supported first-level capabilities.", nameof(capability));
        }

        if (resultId == Guid.Empty)
        {
            throw new ArgumentException("Result identity cannot be empty.", nameof(resultId));
        }

        Capability = capability;
        ResultId = resultId;
    }
}
