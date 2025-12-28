namespace CaptureTool.Domains.Capture.Interfaces.Metadata;

/// <summary>
/// Defines the type of data a metadata scanner processes.
/// </summary>
public enum MetadataScannerType
{
    /// <summary>
    /// Scanner processes video frame data.
    /// </summary>
    Video,

    /// <summary>
    /// Scanner processes audio sample data.
    /// </summary>
    Audio
}
