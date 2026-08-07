using CaptureTool.Application.Abstractions.Analysis.Persistence;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Text.Json;

namespace CaptureTool.Infrastructure.Analysis.Persistence.Serialization;

internal sealed class CaptureAnalysisControlDocument
{
    public int SchemaVersion { get; set; }

    public long DocumentRevision { get; set; }

    public CaptureAnalysisPolicyDocument Policy { get; set; } = new();

    public List<CaptureAnalysisEnrollmentDocument> Enrollments { get; set; } = [];
}

internal sealed class CaptureAnalysisPolicyDocument
{
    public CaptureAnalysisConsentState ConsentState { get; set; }

    public long PolicyRevision { get; set; }

    public long ControlGeneration { get; set; }

    public CaptureAnalysisAuthorizationScopeDocument? AuthorizationScope { get; set; }

    public bool IsFutureCaptureAdmissionEnabled { get; set; }

    public long FutureCaptureSequenceWatermark { get; set; }

    public CaptureAnalysisBackfillState BackfillState { get; set; }

    public long BackfillUpperSequence { get; set; }

    public long BackfillCheckpoint { get; set; }
}

internal sealed class CaptureAnalysisAuthorizationScopeDocument
{
    public AnalysisPurposeDocument Purpose { get; set; } = new();

    public AnalysisProcessingPolicyDocument ProcessingPolicy { get; set; } = new();

    public List<CapabilityDefinitionDocument> Capabilities { get; set; } = [];
}

internal sealed class AnalysisPurposeDocument
{
    public string Id { get; set; } = string.Empty;

    public int Version { get; set; }
}

internal sealed class AnalysisProcessingPolicyDocument
{
    public AnalysisPurposeDocument AuthorizedPurpose { get; set; } = new();

    public List<ProcessingBoundary> AllowedBoundaries { get; set; } = [];

    public List<string> AllowedRemoteProviderIds { get; set; } = [];
}

internal sealed class CaptureAnalysisEnrollmentDocument
{
    public string CaptureId { get; set; } = string.Empty;

    public CaptureAnalysisEnrollmentState State { get; set; }

    public CaptureAnalysisExclusionReason ExclusionReason { get; set; }

    public long EnrollmentGeneration { get; set; }

    public long TombstoneGeneration { get; set; }

    public long AssetFinalizationSequence { get; set; }

    public string? RequestedRecipeId { get; set; }

    public int? RequestedRecipeVersion { get; set; }
}

internal sealed class CaptureAnalysisEnvelopeDocument
{
    public int SchemaVersion { get; set; }

    public long DocumentRevision { get; set; }

    public string CaptureId { get; set; } = string.Empty;

    public CaptureMediaKind MediaKind { get; set; }

    public DateTimeOffset CapturedAtUtc { get; set; }

    public SourceRevisionDocument SourceRevision { get; set; } = new();

    public CaptureAnalysisRecipeDocument Recipe { get; set; } = new();

    public List<JsonElement> CapabilityEntries { get; set; } = [];
}

internal sealed class SourceRevisionDocument
{
    public long Length { get; set; }

    public DateTimeOffset LastWriteTimeUtc { get; set; }

    public string FingerprintAlgorithm { get; set; } = string.Empty;

    public string FingerprintValue { get; set; } = string.Empty;
}

internal sealed class CaptureAnalysisRecipeDocument
{
    public string Id { get; set; } = string.Empty;

    public int Version { get; set; }

    public CaptureMediaKind MediaKind { get; set; }

    public List<RecipeCapabilityDocument> Capabilities { get; set; } = [];
}

internal sealed class RecipeCapabilityDocument
{
    public CapabilityDefinitionDocument Capability { get; set; } = new();

    public RecipeCapabilityRequirement Requirement { get; set; }
}

internal sealed class CapabilityDefinitionDocument
{
    public string Id { get; set; } = string.Empty;

    public int SchemaVersion { get; set; }

    public CapabilityResultClassification Classification { get; set; }
}

internal sealed class CaptureAnalysisCapabilityEntryDocument
{
    public CapabilityDefinitionDocument Capability { get; set; } = new();

    public CanonicalCapabilityResultDocument? CanonicalResult { get; set; }

    public CapabilityOutcomeDocument? LatestOutcome { get; set; }
}

internal sealed class CanonicalCapabilityResultDocument
{
    public AnalyzerIdentityDocument Analyzer { get; set; } = new();

    public ProcessingBoundary ProcessingBoundary { get; set; }

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public JsonElement Payload { get; set; }
}

internal sealed class CapabilityOutcomeDocument
{
    public AnalyzerIdentityDocument Analyzer { get; set; } = new();

    public ProcessingBoundary ProcessingBoundary { get; set; }

    public CapabilityOutcomeState State { get; set; }

    public AnalysisFailureDocument Failure { get; set; } = new();

    public DateTimeOffset GeneratedAtUtc { get; set; }
}

internal sealed class AnalyzerIdentityDocument
{
    public string AnalyzerId { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public string? ModelId { get; set; }

    public string? ModelVersion { get; set; }

    public string AdapterVersion { get; set; } = string.Empty;

    public string? RuntimeId { get; set; }

    public string? RuntimeVersion { get; set; }

    public string? PackageVersion { get; set; }

    public string? ConfigurationFingerprint { get; set; }
}

internal sealed class AnalysisFailureDocument
{
    public AnalysisFailureCode Code { get; set; }

    public AnalysisFailureDisposition Disposition { get; set; }
}

internal sealed class MediaPropertiesPayloadDocument
{
    public CaptureMediaKind MediaKind { get; set; }

    public PixelSizeDocument? PixelSize { get; set; }

    public long? DurationTicks { get; set; }

    public string? MimeType { get; set; }

    public string? Container { get; set; }

    public string? VideoCodec { get; set; }

    public string? AudioCodec { get; set; }

    public int? AudioChannelCount { get; set; }

    public int? SampleRateHz { get; set; }

    public long? BitRate { get; set; }

    public double? FrameRate { get; set; }
}

internal sealed class PixelSizeDocument
{
    public int Width { get; set; }

    public int Height { get; set; }
}

internal sealed class OcrDocumentPayloadDocument
{
    public PixelSizeDocument RasterSize { get; set; } = new();

    public string FullText { get; set; } = string.Empty;

    public List<OcrLanguageCandidateDocument> Languages { get; set; } = [];

    public List<OcrRegionDocument> Regions { get; set; } = [];
}

internal sealed class OcrLanguageCandidateDocument
{
    public string LanguageTag { get; set; } = string.Empty;

    public double? Confidence { get; set; }
}

internal sealed class OcrRegionDocument
{
    public PixelRectDocument Bounds { get; set; } = new();

    public List<OcrLineDocument> Lines { get; set; } = [];

    public double? Confidence { get; set; }
}

internal sealed class OcrLineDocument
{
    public string Text { get; set; } = string.Empty;

    public PixelRectDocument Bounds { get; set; } = new();

    public List<OcrWordDocument> Words { get; set; } = [];

    public double? Confidence { get; set; }
}

internal sealed class OcrWordDocument
{
    public string Text { get; set; } = string.Empty;

    public PixelRectDocument Bounds { get; set; } = new();

    public double? Confidence { get; set; }
}

internal sealed class PixelRectDocument
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}

internal sealed class ImageDescriptionPayloadDocument
{
    public string Description { get; set; } = string.Empty;

    public ImageDescriptionPurpose Purpose { get; set; }

    public string? Style { get; set; }

    public double? Confidence { get; set; }
}
