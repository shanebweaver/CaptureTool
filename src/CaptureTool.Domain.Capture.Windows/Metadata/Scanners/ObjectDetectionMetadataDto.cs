namespace CaptureTool.Domain.Capture.Windows.Metadata.Scanners;

internal sealed class ObjectDetectionMetadataDto
{
    public string Label { get; set; } = string.Empty;

    public float Confidence { get; set; }

    public ObjectDetectionBoxMetadataDto Box { get; set; } = new();
}

internal sealed class ObjectDetectionBoxMetadataDto
{
    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }
}
