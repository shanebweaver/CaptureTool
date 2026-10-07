using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Storage;
using Moq;

namespace CaptureTool.Infrastructure.Edit.Windows.Tests;

[TestClass]
public sealed class UnsupportedDeviceTests
{
    [TestMethod]
    public async Task EditingProvidersRejectUnsupportedPlatformsBeforePreparation()
    {
        var device = Mock.Of<IAiDeviceSupport>();
        var scratch = new Mock<IScratchArtifactStore>(MockBehavior.Strict);
        var description = new WindowsImageDescriptionService(device);
        var foreground = new WindowsImageForegroundExtractionService(scratch.Object, device);
        var erase = new WindowsImageObjectEraseService(scratch.Object, device);
        var image = new WindowsImageSuperResolutionService(scratch.Object, device);
        var video = new WindowsVideoSuperResolutionService(scratch.Object, device);
        Assert.AreEqual(ImageDescriptionReadyState.NotSupported, description.GetReadyState());
        Assert.AreEqual(ForegroundExtractionReadyState.NotSupported, foreground.GetReadyState());
        Assert.AreEqual(ObjectEraseReadyState.NotSupported, erase.GetReadyState());
        Assert.AreEqual(ImageSuperResolutionReadyState.NotSupported, image.GetReadyState());
        Assert.AreEqual(VideoSuperResolutionReadyState.NotSupported, video.GetReadyState());
        Assert.AreEqual(ImageDescriptionPreparationResult.NotSupported, await description.EnsureReadyAsync());
        Assert.AreEqual(ForegroundExtractionPreparationResult.NotSupported, await foreground.EnsureReadyAsync());
        Assert.AreEqual(ObjectErasePreparationResult.NotSupported, await erase.EnsureReadyAsync());
        Assert.AreEqual(ImageSuperResolutionPreparationResult.NotSupported, await image.EnsureReadyAsync());
        Assert.AreEqual(VideoSuperResolutionPreparationResult.NotSupported, await video.EnsureReadyAsync());
        scratch.VerifyNoOtherCalls();
    }
}
