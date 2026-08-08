using CaptureTool.Domain.Capture;

namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

internal sealed class CaptureAssetCatalogDocument
{
    public int SchemaVersion { get; set; }

    public long LastSequence { get; set; }

    public List<CaptureAssetDocument> Assets { get; set; } = [];

    public List<CaptureAssetChangeDocument> Changes { get; set; } = [];
}

internal sealed class CaptureAssetDocument
{
    public string CaptureId { get; set; } = string.Empty;

    public CaptureFileType MediaType { get; set; }

    public string RetainedSourcePath { get; set; } = string.Empty;

    public CaptureSourceOwnership SourceOwnership { get; set; }

    public string? PreferredOpenPath { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public CaptureAssetLifecycleState LifecycleState { get; set; }

    public long LifecycleRevision { get; set; }
}

internal sealed class CaptureAssetChangeDocument
{
    public long Sequence { get; set; }

    public string CaptureId { get; set; } = string.Empty;

    public long LifecycleRevision { get; set; }

    public CaptureAssetChangeType ChangeType { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }
}
