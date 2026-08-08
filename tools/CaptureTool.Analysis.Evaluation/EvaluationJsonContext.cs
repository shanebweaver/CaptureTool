using System.Text.Json.Serialization;

namespace CaptureTool.Analysis.Evaluation;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(EvaluationCorpus))]
[JsonSerializable(typeof(ProviderEvaluationRun))]
[JsonSerializable(typeof(EvaluationReport))]
public sealed partial class EvaluationJsonContext : JsonSerializerContext;
