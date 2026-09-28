using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

/// <summary>Model-specific prompts and bounded parsing. No captured text or raw model response is logged.</summary>
internal static class FoundryMetadataProtocol
{
    public const int MaximumResponseBytes = 65536;
    public static bool CanCorrect(AnalyzerOutcome outcome) => MetadataTextProtocol.CanCorrect(outcome);

    public static byte[] CreateRequest(string modelId, MetadataProcessorInput input, bool correction = false)
    {
        string sources = MetadataTextProtocol.CreateSources(input);
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject(); json.WriteString("model", modelId); json.WriteBoolean("stream", false);
            json.WriteBoolean("store", false); json.WriteNumber("temperature", 0); json.WriteNumber("max_tokens", 1024);
            json.WritePropertyName("response_format"); json.WriteStartObject(); json.WriteString("type", "json_schema");
            json.WritePropertyName("json_schema"); json.WriteStartObject(); json.WriteString("name", input.Descriptor.Capability.Name.Replace('-', '_')); json.WriteBoolean("strict", true);
            json.WritePropertyName("schema");
            string schemaText = input.Descriptor.Capability == AnalysisCapability.CaptureSynopsis ? SynopsisSchema :
                input.Descriptor.Capability == AnalysisCapability.CaptureName ? NameSchema :
                input.Descriptor.Capability == AnalysisCapability.ImageAltText ? AltTextSchema : ClassificationSchema;
            // Foundry Local 1.2.4's schema transport accepts string enums only. Use numeric
            // bounds for citation IDs; the parser independently verifies every reference.
            schemaText = schemaText.Replace("LAST_SOURCE_ID", Math.Max(0, input.Entries.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            using (var schema = JsonDocument.Parse(schemaText))
            {
                schema.RootElement.WriteTo(json);
            }

            json.WriteEndObject(); json.WriteEndObject();
            json.WritePropertyName("messages"); json.WriteStartArray();
            Message("system", MetadataTextProtocol.CreateInstructions(input, correction, disableThinking: true));
            Message("user", sources);
            json.WriteEndArray(); json.WriteEndObject();
            void Message(string role, string text)
            {
                json.WriteStartObject(); json.WriteString("role", role); json.WriteString("content", text); json.WriteEndObject();
            }
        }
        return buffer.ToArray();
    }

    public static AnalyzerOutcome ParseResponse(ReadOnlyMemory<byte> bytes, string modelId, MetadataProcessorInput input, AnalyzerProvenance producer)
    {
        if (bytes.Length > MaximumResponseBytes)
        {
            return Rejected("response-size");
        }

        string stage = "envelope";
        try
        {
            using var response = JsonDocument.Parse(bytes, new() { MaxDepth = 16 });
            JsonElement root = response.RootElement;
            if (root.GetProperty("model").GetString() != modelId || root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
            {
                return Rejected("model-or-error");
            }

            JsonElement choices = root.GetProperty("choices");
            if (choices.GetArrayLength() != 1)
            {
                return Rejected("choices");
            }

            JsonElement choice = choices[0];
            JsonElement message = choice.GetProperty("message");
            if (choice.GetProperty("finish_reason").GetString() == "length")
            {
                return Rejected("truncated");
            }

            if (message.GetProperty("role").GetString() != "assistant" || choice.GetProperty("finish_reason").GetString() != "stop" ||
                message.TryGetProperty("tool_calls", out var tools) && tools.ValueKind != JsonValueKind.Null && tools.GetArrayLength() != 0)
            {
                return Rejected("role-or-tools");
            }

            if (message.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(refusal.GetString()))
            {
                return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.ContentRejected, "text-response-refused");
            }

            string text = message.GetProperty("content").GetString()?.Trim() ?? string.Empty;
            return MetadataTextProtocol.ParseText(text, input, producer);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or
            ArgumentException or InvalidDataException or FormatException or OverflowException)
        {
            return Rejected(stage);
        }
    }

    private static AnalyzerOutcome Rejected(string reason) => MetadataTextProtocol.Rejected(reason);

    private const string SuggestionSchema = """
        {"type":"object","additionalProperties":false,"required":["text","evidence"],"properties":{
          "text":{"type":"string","minLength":1,"maxLength":400},"evidence":{"type":"integer","minimum":0,"maximum":LAST_SOURCE_ID}}}
        """;
    private const string SynopsisSchema = """
        {"type":"object","additionalProperties":false,"required":["summary","evidence"],"properties":{
          "summary":{"anyOf":[{"type":"null"},{"type":"string","minLength":1,"maxLength":400}]},
          "evidence":{"anyOf":[{"type":"null"},{"type":"integer","minimum":0,"maximum":LAST_SOURCE_ID}]}}}
        """;
    private const string NameSchema = """
        {"type":"object","additionalProperties":false,"required":["name","evidence"],"properties":{
          "name":{"anyOf":[{"type":"null"},{"type":"string","minLength":1,"maxLength":160}]},
          "evidence":{"anyOf":[{"type":"null"},{"type":"integer","minimum":0,"maximum":LAST_SOURCE_ID}]}}}
        """;
    private const string AltTextSchema = """
        {"type":"object","additionalProperties":false,"required":["altText","evidence"],"properties":{
          "altText":{"anyOf":[{"type":"null"},{"type":"string","minLength":1,"maxLength":400}]},
          "evidence":{"anyOf":[{"type":"null"},{"type":"integer","minimum":0,"maximum":LAST_SOURCE_ID}]}}}
        """;
    private static readonly string ClassificationSchema = """
        {"type":"object","additionalProperties":false,"required":["category","evidence","topics"],"properties":{
          "category":{"enum":[null,"document","conversation","code","error","web-content","media","other"]},
          "evidence":{"anyOf":[{"type":"null"},{"type":"integer","minimum":0,"maximum":LAST_SOURCE_ID}]},"topics":{"type":"array","maxItems":5,"items":SUGGESTION}}}
        """.Replace("SUGGESTION", SuggestionSchema.Replace("\"maxLength\":400", "\"maxLength\":48", StringComparison.Ordinal), StringComparison.Ordinal);
}
