using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.RecentCaptures.Serialization;

[JsonSourceGenerationOptions(Converters = [typeof(CaptureIdJsonConverter)])]
[JsonSerializable(typeof(RecentCaptureCatalogEnvelope))]
[JsonSerializable(typeof(List<RecentCaptureCatalogEntry>))]
[JsonSerializable(typeof(List<string>))]
internal sealed partial class RecentCaptureCatalogContext : JsonSerializerContext { }
