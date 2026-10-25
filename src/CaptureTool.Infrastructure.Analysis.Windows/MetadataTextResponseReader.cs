using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Validates model text against the requested payload schema and supplied evidence.</summary>
internal static class MetadataTextResponseReader
{
    public static AnalyzerOutcome Parse(string text, MetadataProcessorInput input, AnalyzerProvenance producer)
    {
        text = text.Trim();
        if (text.Length is 0 or > 16000)
        {
            return MetadataTextProtocol.Rejected("content-size");
        }

        // A single enclosing Markdown fence is presentation, not payload. Its contents
        // still go through the exact JSON, field and evidence validation.
        if (text.StartsWith("```json\n", StringComparison.Ordinal) || text.StartsWith("```json\r\n", StringComparison.Ordinal))
        {
            if (!text.EndsWith("\n```", StringComparison.Ordinal))
            {
                return MetadataTextProtocol.Rejected("json");
            }
            text = text[7..^3].Trim();
        }

        string stage = "json";
        try
        {
            using var output = JsonDocument.Parse(text, new() { MaxDepth = 8 });
            stage = "schema";
            AnalysisPayload payload = ReadPayload(output.RootElement, input);
            return AnalyzerOutcome.Success(payload, producer);
        }
        catch (InvalidOutputException exception)
        {
            return MetadataTextProtocol.Rejected(exception.Code);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or
            ArgumentException or InvalidDataException or FormatException or OverflowException)
        {
            return MetadataTextProtocol.Rejected(stage);
        }
    }

    private static AnalysisPayload ReadPayload(JsonElement value, MetadataProcessorInput input)
    {
        AnalysisCapability capability = input.Descriptor.Capability;
        if (capability == AnalysisCapability.CaptureSynopsis)
        {
            return ReadSynopsis(value, input);
        }
        if (capability == AnalysisCapability.CaptureName)
        {
            SuggestedText? suggestion = ReadOptionalSuggestion(value, "name", input);
            if (suggestion?.Text.Length > 160)
            {
                throw new InvalidOutputException("text-bounds");
            }
            return new CaptureNameMetadata(suggestion, input.Coverage);
        }
        if (capability == AnalysisCapability.ImageAltText)
        {
            SuggestedText? suggestion = ReadOptionalSuggestion(value, "altText", input);
            return new ImageAltTextMetadata(suggestion, input.Coverage);
        }
        if (capability == AnalysisCapability.CaptureClassification)
        {
            return ReadClassification(value, input);
        }
        throw new InvalidOutputException("capability");
    }

    private static CaptureSynopsisMetadata ReadSynopsis(JsonElement value, MetadataProcessorInput input)
    {
        RequireFields(value, "summary", "evidence");
        string? summary = value.GetProperty("summary").GetString();
        AnalysisEvidence[] evidence = ReadEvidence(value.GetProperty("evidence"), input);
        RequireEvidenceCount(evidence, summary == null ? 0 : 1);
        SuggestedText[] suggestions = summary == null ? [] : [new SuggestedText(summary, evidence)];
        return new CaptureSynopsisMetadata(suggestions, input.Coverage);
    }

    private static SuggestedText? ReadOptionalSuggestion(JsonElement value, string textField, MetadataProcessorInput input)
    {
        RequireFields(value, textField, "evidence");
        JsonElement textValue = value.GetProperty(textField);
        JsonElement evidenceValue = value.GetProperty("evidence");
        if (textValue.ValueKind == JsonValueKind.Null)
        {
            if (evidenceValue.ValueKind != JsonValueKind.Null)
            {
                throw new InvalidOutputException("evidence");
            }
            return null;
        }

        string text = textValue.GetString() ?? throw new InvalidDataException("Missing suggestion.");
        AnalysisEvidence[] evidence = ReadEvidence(evidenceValue, input);
        RequireEvidenceCount(evidence, 1);
        try
        {
            return new SuggestedText(text, evidence);
        }
        catch (ArgumentException)
        {
            throw new InvalidOutputException("text-bounds");
        }
    }

    private static CaptureClassificationMetadata ReadClassification(JsonElement value, MetadataProcessorInput input)
    {
        RequireFields(value, "category", "evidence", "topics");
        CaptureCategory? category = ReadCategory(value.GetProperty("category"));
        AnalysisEvidence[] evidence = ReadEvidence(value.GetProperty("evidence"), input);
        RequireEvidenceCount(evidence, category == null ? 0 : 1);

        string[] topics = value.GetProperty("topics").EnumerateArray()
            .Select(item => item.GetString()?.ToLowerInvariant() ?? throw new InvalidOutputException("topics"))
            .ToArray();
        bool hasTopicsWithoutCategory = category == null && topics.Length != 0;
        if (hasTopicsWithoutCategory || topics.Any(topic => topic.Length > 48))
        {
            throw new InvalidOutputException("topics");
        }

        // Validate every topic before deduplicating or limiting the selection. This
        // cannot invent content and must not hide invalid topics past the limit.
        SuggestedText[] suggestions = topics.Select(topic => new SuggestedText(topic, evidence)).ToArray();
        return new CaptureClassificationMetadata(category, evidence,
            suggestions.DistinctBy(topic => topic.Text).Take(5), input.Coverage);
    }

    private static CaptureCategory? ReadCategory(JsonElement value) => value.GetString() switch
    {
        null => null,
        "document" => CaptureCategory.Document,
        "conversation" => CaptureCategory.Conversation,
        "code" => CaptureCategory.Code,
        "error" => CaptureCategory.Error,
        "web-content" => CaptureCategory.WebContent,
        "media" => CaptureCategory.Media,
        "other" => CaptureCategory.Other,
        _ => throw new InvalidDataException("Unknown category."),
    };

    private static AnalysisEvidence[] ReadEvidence(JsonElement value, MetadataProcessorInput input)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int index) || index < 0 || index >= input.Entries.Count)
        {
            throw new InvalidOutputException("evidence");
        }

        MetadataTextEntry entry = input.Entries[index];
        return [new AnalysisEvidence(entry.ResultId, entry.EntryIndex, 0, entry.Text.Length)];
    }

    private static void RequireEvidenceCount(AnalysisEvidence[] evidence, int expected)
    {
        if (evidence.Length != expected)
        {
            throw new InvalidOutputException("evidence");
        }
    }

    private static void RequireFields(JsonElement value, params string[] expected)
    {
        string[] actual = value.EnumerateObject().Select(property => property.Name).ToArray();
        bool hasDuplicates = actual.Distinct(StringComparer.Ordinal).Count() != actual.Length;
        bool hasUnknownFields = actual.Except(expected, StringComparer.Ordinal).Any();
        if (actual.Length != expected.Length || hasDuplicates || hasUnknownFields)
        {
            throw new InvalidOutputException("fields");
        }
    }

    // Codes are a closed set and contain neither captured text nor model responses.
    private sealed class InvalidOutputException : Exception
    {
        public string Code { get; }

        public InvalidOutputException(string code) => Code = code;
    }
}
