using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Domain.FileSystem;
using System.Drawing;
using VideoPreparationResult = CaptureTool.Application.Abstractions.Edit.Video.SuperResolution.VideoSuperResolutionPreparationResult;
using VideoPreparationStatus = CaptureTool.Application.Abstractions.Edit.Video.SuperResolution.VideoSuperResolutionPreparationStatus;
using VideoResult = CaptureTool.Application.Abstractions.Edit.Video.SuperResolution.VideoSuperResolutionResult;
using VideoStatus = CaptureTool.Application.Abstractions.Edit.Video.SuperResolution.VideoSuperResolutionStatus;

namespace CaptureTool.Application.Tests.Edit;

[TestClass]
public sealed class AiResultContractTests
{
    [TestMethod]
    public void PreparationResults_ExposeEveryOutcome()
    {
        Assert.AreEqual(ImageDescriptionPreparationStatus.Success, ImageDescriptionPreparationResult.Success.Status);
        Assert.AreEqual(ImageDescriptionPreparationStatus.Cancelled, ImageDescriptionPreparationResult.Cancelled.Status);
        Assert.AreEqual(ImageDescriptionPreparationStatus.NotSupported, ImageDescriptionPreparationResult.NotSupported.Status);
        Assert.AreEqual("description", ImageDescriptionPreparationResult.Failed("description").ErrorMessage);

        Assert.AreEqual(TextExtractionPreparationStatus.Success, TextExtractionPreparationResult.Success.Status);
        Assert.AreEqual(TextExtractionPreparationStatus.Cancelled, TextExtractionPreparationResult.Cancelled.Status);
        Assert.AreEqual(TextExtractionPreparationStatus.NotSupported, TextExtractionPreparationResult.NotSupported.Status);
        Assert.AreEqual("text", TextExtractionPreparationResult.Failed("text").ErrorMessage);

        Assert.AreEqual(ObjectErasePreparationStatus.Success, ObjectErasePreparationResult.Success.Status);
        Assert.AreEqual(ObjectErasePreparationStatus.Cancelled, ObjectErasePreparationResult.Cancelled.Status);
        Assert.AreEqual(ObjectErasePreparationStatus.NotSupported, ObjectErasePreparationResult.NotSupported.Status);
        Assert.AreEqual("erase", ObjectErasePreparationResult.Failed("erase").ErrorMessage);

        Assert.AreEqual(ImageSuperResolutionPreparationStatus.Success, ImageSuperResolutionPreparationResult.Success.Status);
        Assert.AreEqual(ImageSuperResolutionPreparationStatus.Cancelled, ImageSuperResolutionPreparationResult.Cancelled.Status);
        Assert.AreEqual(ImageSuperResolutionPreparationStatus.NotSupported, ImageSuperResolutionPreparationResult.NotSupported.Status);
        Assert.AreEqual("image", ImageSuperResolutionPreparationResult.Failed("image").ErrorMessage);

        Assert.AreEqual(VideoPreparationStatus.Success, VideoPreparationResult.Success.Status);
        Assert.AreEqual(VideoPreparationStatus.Cancelled, VideoPreparationResult.Cancelled.Status);
        Assert.AreEqual(VideoPreparationStatus.NotSupported, VideoPreparationResult.NotSupported.Status);
        Assert.AreEqual("video", VideoPreparationResult.Failed("video").ErrorMessage);
    }

    [TestMethod]
    public void OperationResults_ExposeEveryOutcomeAndPayload()
    {
        var imageFile = new ImageFile("image.png");
        var videoFile = new VideoFile("video.mp4");
        var document = new RecognizedTextDocument("text", new Size(100, 50), []);

        Assert.AreEqual("description", ImageDescriptionResult.Success("description").Description);
        Assert.AreEqual(ImageDescriptionStatus.Cancelled, ImageDescriptionResult.Cancelled.Status);
        Assert.AreEqual(ImageDescriptionStatus.NotReady, ImageDescriptionResult.NotReady.Status);
        Assert.AreEqual(ImageDescriptionStatus.NotSupported, ImageDescriptionResult.NotSupported.Status);
        Assert.AreEqual(ImageDescriptionStatus.BlockedByPolicy, ImageDescriptionResult.BlockedByPolicy.Status);
        Assert.AreEqual(ImageDescriptionStatus.BlockedByContentSafety, ImageDescriptionResult.BlockedByContentSafety.Status);
        Assert.AreEqual(ImageDescriptionStatus.TooMuchText, ImageDescriptionResult.TooMuchText.Status);
        Assert.AreEqual("description failed", ImageDescriptionResult.Failed("description failed").ErrorMessage);

        Assert.AreSame(document, TextExtractionResult.Success(document).Document);
        Assert.AreEqual(TextExtractionStatus.Cancelled, TextExtractionResult.Cancelled.Status);
        Assert.AreEqual(TextExtractionStatus.NotSupported, TextExtractionResult.NotSupported.Status);
        Assert.AreEqual(TextExtractionStatus.NotReady, TextExtractionResult.NotReady.Status);
        Assert.AreEqual("too large", TextExtractionResult.TooLarge("too large").ErrorMessage);
        Assert.AreEqual("text failed", TextExtractionResult.Failed("text failed").ErrorMessage);

        Assert.AreSame(imageFile, ObjectEraseResult.Success(imageFile).ImageFile);
        Assert.AreEqual(ObjectEraseStatus.Cancelled, ObjectEraseResult.Cancelled.Status);
        Assert.AreEqual(ObjectEraseStatus.NotReady, ObjectEraseResult.NotReady.Status);
        Assert.AreEqual(ObjectEraseStatus.NotSupported, ObjectEraseResult.NotSupported.Status);
        Assert.AreEqual("erase failed", ObjectEraseResult.Failed("erase failed").ErrorMessage);

        Assert.AreSame(imageFile, ImageSuperResolutionResult.Success(imageFile, new Size(200, 100)).ImageFile);
        Assert.AreEqual(ImageSuperResolutionStatus.Cancelled, ImageSuperResolutionResult.Cancelled.Status);
        Assert.AreEqual(ImageSuperResolutionStatus.NotSupported, ImageSuperResolutionResult.NotSupported.Status);
        Assert.AreEqual(ImageSuperResolutionStatus.NotReady, ImageSuperResolutionResult.NotReady.Status);
        Assert.AreEqual("too large", ImageSuperResolutionResult.TooLarge("too large").ErrorMessage);
        Assert.AreEqual("image failed", ImageSuperResolutionResult.Failed("image failed").ErrorMessage);

        Assert.AreSame(videoFile, VideoResult.Success(videoFile).VideoFile);
        Assert.AreEqual(VideoStatus.Cancelled, VideoResult.Cancelled.Status);
        Assert.AreEqual(VideoStatus.NotSupported, VideoResult.NotSupported.Status);
        Assert.AreEqual(VideoStatus.NotReady, VideoResult.NotReady.Status);
        Assert.AreEqual("unsupported", VideoResult.UnsupportedVideo("unsupported").ErrorMessage);
        Assert.AreEqual("video failed", VideoResult.Failed("video failed").ErrorMessage);
    }
}
