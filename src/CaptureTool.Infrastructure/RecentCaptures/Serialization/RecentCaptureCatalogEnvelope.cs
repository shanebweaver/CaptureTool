using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.RecentCaptures.Serialization;

internal sealed class RecentCaptureCatalogEnvelope
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("assetChangeCheckpoint")]
    public long AssetChangeCheckpoint { get; set; }

    [JsonPropertyName("appliedOutOfOrderSequences")]
    public List<long> AppliedOutOfOrderSequences { get; set; } = [];

    [JsonPropertyName("entries")]
    public List<RecentCaptureCatalogEntry> Entries { get; set; } = [];

    [JsonPropertyName("protectedRetainedCaptureRecoveryExclusions")]
    public string? ProtectedRetainedCaptureRecoveryExclusions { get; set; }

    [JsonPropertyName("retainedCaptureRecoveryDisabled")]
    public bool RetainedCaptureRecoveryDisabled { get; set; }
}
