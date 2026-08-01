using CaptureTool.Application.Abstractions.Library.RecentCaptures;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.RecentCaptures.Serialization;

[JsonSerializable(typeof(List<RecentCaptureCatalogEntry>))]
internal sealed partial class RecentCaptureCatalogContext : JsonSerializerContext { }
