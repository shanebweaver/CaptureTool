namespace CaptureTool.Domain.Analysis;

public readonly record struct AnalysisCapabilityId
{
    public AnalysisCapabilityId(string value)
    {
        Value = BoundedIdentifier.Normalize(value, nameof(value));
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString()
    {
        return Value;
    }

}

public readonly record struct CapabilitySchemaVersion
{
    public CapabilitySchemaVersion(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "A capability schema version must be positive.");
        }

        Value = value;
    }

    public int Value { get; }

    public bool IsEmpty => Value <= 0;

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public enum CapabilityResultClassification
{
    Unknown,
    Observation,
    MachineExtracted,
    Inference,
    DisposableRepresentation
}

public readonly record struct CapabilityDefinition
{
    public CapabilityDefinition(
        AnalysisCapabilityId id,
        CapabilitySchemaVersion schemaVersion,
        CapabilityResultClassification classification)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("A capability definition requires an ID.", nameof(id));
        }

        if (schemaVersion.IsEmpty)
        {
            throw new ArgumentException("A capability definition requires a schema version.", nameof(schemaVersion));
        }

        if (!Enum.IsDefined(classification) || classification == CapabilityResultClassification.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(classification));
        }

        Id = id;
        SchemaVersion = schemaVersion;
        Classification = classification;
    }

    public AnalysisCapabilityId Id { get; }

    public CapabilitySchemaVersion SchemaVersion { get; }

    public CapabilityResultClassification Classification { get; }
}

public static class AnalysisCapabilities
{
    public static CapabilityDefinition MediaPropertiesV1 { get; } = new(
        new AnalysisCapabilityId("media-properties"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.Observation);

    public static CapabilityDefinition OcrDocumentV1 { get; } = new(
        new AnalysisCapabilityId("ocr-document"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.MachineExtracted);

    public static CapabilityDefinition ImageDescriptionV1 { get; } = new(
        new AnalysisCapabilityId("image-description"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.Inference);

    public static CapabilityDefinition SpeechTranscriptV1 { get; } = new(
        new AnalysisCapabilityId("speech-transcript"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.MachineExtracted);

    public static CapabilityDefinition VideoOcrTrackV1 { get; } = new(
        new AnalysisCapabilityId("video-ocr-track"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.MachineExtracted);

    public static CapabilityDefinition VideoDescriptionTrackV1 { get; } = new(
        new AnalysisCapabilityId("video-description-track"),
        new CapabilitySchemaVersion(1),
        CapabilityResultClassification.Inference);
}
