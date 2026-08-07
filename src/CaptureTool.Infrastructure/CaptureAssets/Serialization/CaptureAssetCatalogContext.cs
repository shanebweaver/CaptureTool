using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

[JsonSerializable(typeof(CaptureAssetCatalogDocument))]
internal sealed partial class CaptureAssetCatalogContext : JsonSerializerContext { }
