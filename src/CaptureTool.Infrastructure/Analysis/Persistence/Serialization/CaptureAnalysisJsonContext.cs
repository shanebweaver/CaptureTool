using System.Text.Json.Serialization;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(CaptureAnalysisControlDocument))]
[JsonSerializable(typeof(CaptureAnalysisEnvelopeDocument))]
[JsonSerializable(typeof(CapabilityDefinitionDocument))]
[JsonSerializable(typeof(CaptureAnalysisCapabilityEntryDocument))]
[JsonSerializable(typeof(MediaPropertiesPayloadDocument))]
[JsonSerializable(typeof(OcrDocumentPayloadDocument))]
[JsonSerializable(typeof(ImageDescriptionPayloadDocument))]
[JsonSerializable(typeof(SpeechTranscriptPayloadDocument))]
[JsonSerializable(typeof(VideoOcrTrackPayloadDocument))]
[JsonSerializable(typeof(VideoDescriptionTrackPayloadDocument))]
internal sealed partial class CaptureAnalysisJsonContext : JsonSerializerContext { }
