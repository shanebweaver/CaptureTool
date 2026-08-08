namespace CaptureTool.Domain.Analysis.Payloads;

public enum ImageDescriptionPurpose
{
    Unknown,
    Brief,
    Detailed,
    Diagram,
    Accessible
}

public sealed class ImageDescriptionV1 : CapabilityPayload
{
    public ImageDescriptionV1(
        string description,
        ImageDescriptionPurpose purpose,
        string? style = null,
        double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (description.Length > 4096)
        {
            throw new ArgumentException("An image description cannot exceed 4096 characters.", nameof(description));
        }

        if (!Enum.IsDefined(purpose) || purpose == ImageDescriptionPurpose.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(purpose));
        }

        Description = description.Trim();
        Purpose = purpose;
        Style = PayloadValidation.NormalizeOptional(style, nameof(style));
        Confidence = PayloadValidation.ValidateConfidence(confidence, nameof(confidence));
    }

    public override CapabilityDefinition Definition => AnalysisCapabilities.ImageDescriptionV1;

    public string Description { get; }

    public ImageDescriptionPurpose Purpose { get; }

    public string? Style { get; }

    public double? Confidence { get; }

    public override bool IsEquivalentTo(CapabilityPayload other)
    {
        return other is ImageDescriptionV1 description &&
            string.Equals(Description, description.Description, StringComparison.Ordinal) &&
            Purpose == description.Purpose &&
            string.Equals(Style, description.Style, StringComparison.Ordinal) &&
            Confidence == description.Confidence;
    }
}
