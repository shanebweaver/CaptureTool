using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Orchestration;

public static class CaptureAnalysisRecipeDefaults
{
    public const string CaptureMemoryImageRecipeId = "capture-memory-image";

    public const int CaptureMemoryImageRecipeVersion = 1;

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
}
