namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

internal sealed record CaptureCatalogDocument(int Version, CaptureAssetDocument[] Assets);
internal sealed record CaptureAssetDocument(Guid Id, int MediaType, DateTimeOffset CapturedAt,
    string SourcePath, int SourceOwnership, string? PreferredPath);
