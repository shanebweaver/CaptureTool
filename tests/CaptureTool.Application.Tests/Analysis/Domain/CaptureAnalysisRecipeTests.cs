using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Tests.Analysis.Domain;

[TestClass]
public sealed class CaptureAnalysisRecipeTests
{
    [TestMethod]
    public void Constructor_ShouldDefensivelyCopyUniqueRequiredAndOptionalCapabilities()
    {
        RecipeCapability[] capabilities =
        [
            new(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required),
            new(AnalysisCapabilities.ImageDescriptionV1, RecipeCapabilityRequirement.Optional),
        ];
        var recipe = new CaptureAnalysisRecipe(
            AnalysisTestData.RecipeId,
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            capabilities);

        capabilities[0] = new RecipeCapability(
            AnalysisCapabilities.OcrDocumentV1,
            RecipeCapabilityRequirement.Required);

        Assert.AreEqual(AnalysisCapabilities.MediaPropertiesV1, recipe.Capabilities[0].Capability);
        Assert.IsTrue(recipe.TryGetCapability(
            AnalysisCapabilities.ImageDescriptionV1.Id,
            out RecipeCapability optional));
        Assert.AreEqual(RecipeCapabilityRequirement.Optional, optional.Requirement);
    }

    [TestMethod]
    public void Constructor_ShouldRejectDuplicateCapabilitiesAndUnknownDefaults()
    {
        Assert.ThrowsExactly<ArgumentException>(() => AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Required),
                new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Optional),
            ]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeCapability(
            AnalysisCapabilities.OcrDocumentV1,
            RecipeCapabilityRequirement.Unknown));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CaptureAnalysisRecipe(
            AnalysisTestData.RecipeId,
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Unknown,
            [new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Required)]));
        Assert.ThrowsExactly<ArgumentException>(() => new CaptureAnalysisRecipe(
            AnalysisTestData.RecipeId,
            new AnalysisRecipeVersion(1),
            CaptureMediaKind.Image,
            [default]));
    }

    [TestMethod]
    public void HasSameSemanticsAs_ShouldIgnoreOrderingAndVersionButNotRequirements()
    {
        CaptureAnalysisRecipe first = AnalysisTestData.CreateRecipe(1);
        CaptureAnalysisRecipe reordered = AnalysisTestData.CreateRecipe(
            2,
            new RecipeCapability(AnalysisCapabilities.ImageDescriptionV1, RecipeCapabilityRequirement.Optional),
            new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Required),
            new RecipeCapability(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required));
        CaptureAnalysisRecipe changedRequirement = AnalysisTestData.CreateRecipe(
            2,
            new RecipeCapability(AnalysisCapabilities.MediaPropertiesV1, RecipeCapabilityRequirement.Required),
            new RecipeCapability(AnalysisCapabilities.OcrDocumentV1, RecipeCapabilityRequirement.Optional),
            new RecipeCapability(AnalysisCapabilities.ImageDescriptionV1, RecipeCapabilityRequirement.Optional));

        Assert.IsTrue(first.HasSameSemanticsAs(reordered));
        Assert.IsFalse(first.HasSameSemanticsAs(changedRequirement));
    }
}
