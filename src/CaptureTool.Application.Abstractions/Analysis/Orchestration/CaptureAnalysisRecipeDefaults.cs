using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Orchestration;

public static class CaptureAnalysisRecipeDefaults
{
    public const string CaptureMemoryImageRecipeId = "capture-memory-image";

    public const int CaptureMemoryImageRecipeVersion = 1;

    public const string CaptureMemoryAudioRecipeId = "capture-memory-audio";

    public const int CaptureMemoryAudioRecipeVersion = 1;

    public const string CaptureMemoryVideoRecipeId = "capture-memory-video";

    public const int CaptureMemoryVideoRecipeVersion = 2;

    public static CaptureAnalysisRecipe CreateCaptureMemoryImageRecipe()
    {
        return new(
            new AnalysisRecipeId(CaptureMemoryImageRecipeId),
            new AnalysisRecipeVersion(CaptureMemoryImageRecipeVersion),
            CaptureMediaKind.Image,
            [
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Optional),
            ]);
    }

    public static CaptureAnalysisRecipe CreateCaptureMemoryAudioRecipe()
    {
        return new(
            new AnalysisRecipeId(CaptureMemoryAudioRecipeId),
            new AnalysisRecipeVersion(CaptureMemoryAudioRecipeVersion),
            CaptureMediaKind.Audio,
            [
                new RecipeCapability(
                    AnalysisCapabilities.SpeechTranscriptV1,
                    RecipeCapabilityRequirement.Required),
            ]);
    }

    public static CaptureAnalysisRecipe CreateCaptureMemoryVideoRecipe()
    {
        return new(
            new AnalysisRecipeId(CaptureMemoryVideoRecipeId),
            new AnalysisRecipeVersion(CaptureMemoryVideoRecipeVersion),
            CaptureMediaKind.Video,
            [
                new RecipeCapability(
                    AnalysisCapabilities.VideoOcrTrackV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.SpeechTranscriptV1,
                    RecipeCapabilityRequirement.Optional),
                new RecipeCapability(
                    AnalysisCapabilities.VideoDescriptionTrackV1,
                    RecipeCapabilityRequirement.Optional),
            ]);
    }

    public static bool TryCreateCaptureMemoryRecipe(
        CaptureMediaKind mediaKind,
        out CaptureAnalysisRecipe? recipe)
    {
        recipe = mediaKind switch
        {
            CaptureMediaKind.Image => CreateCaptureMemoryImageRecipe(),
            CaptureMediaKind.Audio => CreateCaptureMemoryAudioRecipe(),
            CaptureMediaKind.Video => CreateCaptureMemoryVideoRecipe(),
            _ => null,
        };
        return recipe != null;
    }
}
