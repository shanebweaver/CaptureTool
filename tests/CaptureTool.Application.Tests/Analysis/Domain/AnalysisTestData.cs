using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Tests.Analysis.Domain;

internal static class AnalysisTestData
{
    public static readonly CaptureId CaptureId = new(new Guid("3c12e543-4918-4382-b5e2-7864aabb5209"));
    public static readonly DateTimeOffset CapturedAtUtc = new(2026, 8, 6, 19, 30, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset GeneratedAtUtc = CapturedAtUtc.AddSeconds(2);
    public static readonly AnalysisPurpose Purpose = new("capture-memory-search", 1);
    public static readonly AnalysisRecipeId RecipeId = new("capture-memory-image");

    public static SourceRevision CreateSource(
        char fingerprintCharacter = 'a',
        long length = 100,
        int timestampOffsetMinutes = 0)
    {
        return new(
            length,
            CapturedAtUtc.AddMinutes(timestampOffsetMinutes),
            ContentFingerprint.Sha256(new string(fingerprintCharacter, 64)));
    }

    public static AnalyzerIdentity CreateAnalyzer(
        string analyzerId = "windows.ocr",
        string providerId = "microsoft.windows",
        char configurationCharacter = 'c')
    {
        return new(
            analyzerId,
            providerId,
            "text-recognizer",
            null,
            "1.0.0",
            "windows-app-sdk",
            null,
            null,
            $"sha256:{new string(configurationCharacter, 64)}");
    }

    public static CaptureAnalysisRecipe CreateRecipe(
        int version = 1,
        params RecipeCapability[]? capabilities)
    {
        RecipeCapability[] requested = capabilities is { Length: > 0 }
            ? capabilities
            :
            [
                new RecipeCapability(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required),
                new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Required),
                new RecipeCapability(AnalysisCapabilities.ImageDescriptionV1, RecipeCapabilityRequirement.Optional),
            ];

        return new(
            RecipeId,
            new AnalysisRecipeVersion(version),
            CaptureMediaKind.Image,
            requested);
    }

    public static AnalysisCommitPreconditions CreatePreconditions(
        SourceRevision? sourceRevision = null,
        CaptureId? captureId = null,
        long captureSourceGeneration = 1,
        AnalysisPurpose? purpose = null,
        long policyRevision = 1,
        long controlGeneration = 1,
        long enrollmentGeneration = 1,
        long tombstoneGeneration = 0,
        AnalysisRecipeId? recipeId = null,
        int recipeVersion = 1,
        long resolutionPolicyRevision = 1)
    {
        SourceRevision source = sourceRevision ?? CreateSource();
        return new(
            captureId ?? CaptureId,
            captureSourceGeneration,
            source.ProvisionalStamp,
            source,
            purpose ?? Purpose,
            policyRevision,
            controlGeneration,
            enrollmentGeneration,
            tombstoneGeneration,
            recipeId ?? RecipeId,
            new AnalysisRecipeVersion(recipeVersion),
            resolutionPolicyRevision);
    }

    public static AnalysisCommitToken CreateToken(
        CapabilityDefinition capability,
        AnalyzerIdentity analyzer,
        AnalysisCommitPreconditions? preconditions = null)
    {
        return new(preconditions ?? CreatePreconditions(), capability, analyzer.Revision);
    }

    public static CanonicalCapabilityResult CreateResult(
        CapabilityPayload payload,
        AnalyzerIdentity analyzer,
        SourceRevision? sourceRevision = null,
        DateTimeOffset? generatedAtUtc = null)
    {
        return new(
            CaptureId,
            sourceRevision ?? CreateSource(),
            payload,
            analyzer,
            ProcessingBoundary.OnDevice,
            generatedAtUtc ?? GeneratedAtUtc);
    }

    public static CaptureAnalysisRecord CreateRecord(
        SourceRevision? sourceRevision = null,
        CaptureAnalysisRecipe? recipe = null)
    {
        return new(
            CaptureId,
            CaptureMediaKind.Image,
            CapturedAtUtc,
            sourceRevision ?? CreateSource(),
            recipe ?? CreateRecipe());
    }
}
