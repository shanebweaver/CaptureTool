namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

internal sealed record CaptureCatalogDocument(int Version, CaptureAssetDocument[] Assets, long Sequence = 0, Guid? NamingEpoch = null);
internal sealed record CaptureAssetDocument(Guid Id, int MediaType, DateTimeOffset? CapturedAt,
    string SourcePath, int SourceOwnership, string? PreferredPath, long Sequence = 0, Guid? AutomaticAuthorization = null,
    string? Name = null, bool AutomaticName = false, Guid? NamingEpoch = null);
