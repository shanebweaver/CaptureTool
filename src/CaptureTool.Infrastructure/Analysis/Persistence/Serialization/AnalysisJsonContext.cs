using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(AnalysisControlDocument))]
[JsonSerializable(typeof(AnalysisDocument))]
internal partial class AnalysisJsonContext : JsonSerializerContext;
