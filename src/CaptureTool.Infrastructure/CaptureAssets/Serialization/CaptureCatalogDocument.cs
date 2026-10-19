namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

internal sealed record CaptureCatalogDocument(int Version, CaptureAssetDocument[] Assets, CaptureRenameDocument? Rename = null);
internal sealed record CaptureRenameDocument(Guid Id, string OldPath, string NewPath);
internal sealed record CaptureAssetDocument(Guid Id, int MediaType, DateTimeOffset? CapturedAt,
    string SourcePath, int SourceOwnership, string? PreferredPath, string? Name = null);
