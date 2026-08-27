using CaptureTool.Application.Abstractions.Analysis.Orchestration;
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

    [TestMethod]
    public void DependencyGraph_ShouldRejectMissingAndCyclicCapabilities()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeCapability(
            AnalysisCapabilities.ImageDescriptionV1,
            RecipeCapabilityRequirement.Optional,
            [AnalysisCapabilities.ImageDescriptionV1]));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeCapability(
            AnalysisCapabilities.ImageDescriptionV1,
            RecipeCapabilityRequirement.Optional,
            [AnalysisCapabilities.OcrDocumentV1, AnalysisCapabilities.OcrDocumentV1]));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeCapability(
            AnalysisCapabilities.ImageDescriptionV1,
            RecipeCapabilityRequirement.Optional,
            [default]));

        Assert.ThrowsExactly<ArgumentException>(() => AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Optional,
                    [AnalysisCapabilities.OcrDocumentV1]),
            ]));

        Assert.ThrowsExactly<ArgumentException>(() => AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Optional),
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Required,
                    [AnalysisCapabilities.OcrDocumentV1]),
            ]));

        Assert.ThrowsExactly<ArgumentException>(() => AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required,
                    [AnalysisCapabilities.OcrDocumentV1]),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Required,
                    [AnalysisCapabilities.MediaPropertiesV1]),
            ]));
    }

    [TestMethod]
    public void GetExecutionOrder_ShouldPlaceDependenciesBeforeConsumers()
    {
        CaptureAnalysisRecipe recipe = AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Optional,
                    [AnalysisCapabilities.OcrDocumentV1]),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Required,
                    [AnalysisCapabilities.MediaPropertiesV1]),
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required),
            ]);

        CollectionAssert.AreEqual(
            new[]
            {
                AnalysisCapabilities.MediaPropertiesV1,
                AnalysisCapabilities.OcrDocumentV1,
                AnalysisCapabilities.ImageDescriptionV1,
            },
            recipe.GetExecutionOrder().Select(capability => capability.Capability).ToArray());

        CaptureAnalysisRecipe withoutDependency = AnalysisTestData.CreateRecipe(
            2,
            new RecipeCapability(
                AnalysisCapabilities.ImageDescriptionV1,
                RecipeCapabilityRequirement.Optional),
            new RecipeCapability(
                AnalysisCapabilities.OcrDocumentV1,
                RecipeCapabilityRequirement.Required),
            new RecipeCapability(
                AnalysisCapabilities.MediaPropertiesV1,
                RecipeCapabilityRequirement.Required));
        Assert.IsFalse(recipe.HasSameSemanticsAs(withoutDependency));
    }

    [TestMethod]
    public void GetExecutionOrder_ShouldPreserveDeclarationOrderAmongReadyCapabilities()
    {
        CaptureAnalysisRecipe recipe = AnalysisTestData.CreateRecipe(
            capabilities:
            [
                new RecipeCapability(
                    AnalysisCapabilities.ImageDescriptionV1,
                    RecipeCapabilityRequirement.Optional,
                    [AnalysisCapabilities.MediaPropertiesV1]),
                new RecipeCapability(
                    AnalysisCapabilities.OcrDocumentV1,
                    RecipeCapabilityRequirement.Required),
                new RecipeCapability(
                    AnalysisCapabilities.MediaPropertiesV1,
                    RecipeCapabilityRequirement.Required),
            ]);

        CollectionAssert.AreEqual(
            new[]
            {
                AnalysisCapabilities.OcrDocumentV1,
                AnalysisCapabilities.MediaPropertiesV1,
                AnalysisCapabilities.ImageDescriptionV1,
            },
            recipe.GetExecutionOrder().Select(capability => capability.Capability).ToArray());
    }

    [TestMethod]
    public void CaptureMemoryDefaults_ShouldResolveImageAudioAndVideoRecipes()
    {
        Assert.IsTrue(CaptureAnalysisRecipeDefaults.TryCreateCaptureMemoryRecipe(
            CaptureMediaKind.Image,
            out CaptureAnalysisRecipe? image));
        Assert.IsTrue(CaptureAnalysisRecipeDefaults.TryCreateCaptureMemoryRecipe(
            CaptureMediaKind.Audio,
            out CaptureAnalysisRecipe? audio));
        Assert.IsTrue(CaptureAnalysisRecipeDefaults.TryCreateCaptureMemoryRecipe(
            CaptureMediaKind.Video,
            out CaptureAnalysisRecipe? video));

        Assert.AreEqual(CaptureMediaKind.Image, image!.MediaKind);
        Assert.AreEqual(CaptureMediaKind.Audio, audio!.MediaKind);
        Assert.AreEqual(AnalysisCapabilities.SpeechTranscriptV1,
            audio.Capabilities.Single().Capability);
        Assert.AreEqual(CaptureMediaKind.Video, video!.MediaKind);
        Assert.AreEqual(2, video.Version.Value);
        Assert.AreEqual(AnalysisCapabilities.VideoOcrTrackV1,
            video.Capabilities.Single(capability =>
                capability.Requirement == RecipeCapabilityRequirement.Required).Capability);
        Assert.AreEqual(RecipeCapabilityRequirement.Optional,
            video.Capabilities.Single(capability =>
                capability.Capability == AnalysisCapabilities.SpeechTranscriptV1).Requirement);
        Assert.AreEqual(RecipeCapabilityRequirement.Optional,
            video.Capabilities.Single(capability =>
                capability.Capability == AnalysisCapabilities.VideoDescriptionTrackV1).Requirement);
    }
}
