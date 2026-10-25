using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.CaptureAssets.Serialization;

// Removing unused fields must not strand otherwise valid capture registrations.
// Required members and the catalog version are still checked when loading.
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(CaptureCatalogDocument))]
internal partial class CaptureCatalogJsonContext : JsonSerializerContext;
