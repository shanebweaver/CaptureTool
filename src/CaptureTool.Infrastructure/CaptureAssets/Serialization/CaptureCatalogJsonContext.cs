using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(CaptureCatalogDocument))]
internal partial class CaptureCatalogJsonContext : JsonSerializerContext;
