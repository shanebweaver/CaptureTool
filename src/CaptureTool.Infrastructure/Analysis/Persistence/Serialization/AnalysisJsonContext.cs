using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

// Extra fields do not invalidate a supported document version. Required members and
// domain validation remain strict; incompatible semantics require a version change.
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(AnalysisControlDocument))]
[JsonSerializable(typeof(AnalysisDocument))]
internal partial class AnalysisJsonContext : JsonSerializerContext;
