using CaptureTool.Application.Abstractions.Metrics;
using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Metrics.Serialization;

[JsonSerializable(typeof(AppMetrics))]
internal sealed partial class AppMetricsContext : JsonSerializerContext { }
