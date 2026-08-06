using System.Text.Json.Serialization;

namespace CaptureTool.Mcp.CaptureServer.Models;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(McpCaptureMetadata))]
[JsonSerializable(typeof(AnnotationPlacementDto[]))]
[JsonSerializable(typeof(MonitorSegmentDto[]))]
[JsonSerializable(typeof(ListWindowsResult))]
internal sealed partial class McpCaptureJsonSerializerContext : JsonSerializerContext;
