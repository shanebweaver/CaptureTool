using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelObjectEraseTests
{
    [TestMethod]
    public async Task ToggleMode_WhenConsentRefused_ShouldRemainInactive()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        var service = new Mock<IImageObjectEraseService>();
        consent
            .Setup(x => x.GetConsentState(AiFeatureId.ImageObjectErase))
            .Returns(AiFeatureConsentState.Denied);
        consent
            .Setup(x => x.SetConsentAsync(AiFeatureId.ImageObjectErase, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        dialog
            .Setup(x => x.RequestConsentAsync(AiFeatureId.ImageObjectErase, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        service.Setup(x => x.GetReadyState()).Returns(ObjectEraseReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object, consent.Object, dialog.Object);
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        await viewModel.ToggleObjectEraseModeCommand.ExecuteAsync(null);

        viewModel.IsObjectEraseModeActive.Should().BeFalse();
        viewModel.CanToggleObjectErase.Should().BeTrue();
        service.Verify(
            x => x.EraseAsync(It.IsAny<ObjectEraseRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ClickingImage_ShouldApplyUndoableObjectEraseResult()
    {
        var service = new Mock<IImageObjectEraseService>();
        service.Setup(x => x.GetReadyState()).Returns(ObjectEraseReadyState.Ready);
        service
            .Setup(x => x.EraseAsync(
                It.Is<ObjectEraseRequest>(request =>
                    request.SourceImage.FilePath == "original.png" &&
                    request.SourceSize == new Size(100, 50) &&
                    request.ObjectPoint == new Point(30, 20)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ObjectEraseResult.Success(new ImageFile("object-erased.png")));

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object);
        int reloadCount = 0;
        viewModel.ReloadCanvasResourcesRequested += (_, _) => reloadCount++;
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleObjectEraseModeCommand.ExecuteAsync(null);

        await viewModel.OnObjectEraseRequestedAsync(new Vector2(30, 20));

        viewModel.ImageFile!.FilePath.Should().Be("object-erased.png");
        viewModel.HasUndoStack.Should().BeTrue();
        viewModel.IsObjectEraseModeActive.Should().BeTrue();
        reloadCount.Should().Be(1);

        viewModel.UndoCommand.Execute(null);
        viewModel.ImageFile!.FilePath.Should().Be("original.png");
        viewModel.HasRedoStack.Should().BeTrue();
        viewModel.IsObjectEraseModeActive.Should().BeFalse();

        viewModel.RedoCommand.Execute(null);
        viewModel.ImageFile!.FilePath.Should().Be("object-erased.png");
        reloadCount.Should().Be(3);
    }

    [TestMethod]
    public async Task ChangingModes_ShouldCancelPendingEraseAndIgnoreItsResult()
    {
        var service = new Mock<IImageObjectEraseService>();
        var pendingResult = new TaskCompletionSource<ObjectEraseResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        service.Setup(x => x.GetReadyState()).Returns(ObjectEraseReadyState.Ready);
        service
            .Setup(x => x.EraseAsync(It.IsAny<ObjectEraseRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ObjectEraseRequest _, CancellationToken token) =>
            {
                requestToken = token;
                return pendingResult.Task;
            });

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object);
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleObjectEraseModeCommand.ExecuteAsync(null);

        Task erase = viewModel.OnObjectEraseRequestedAsync(new Vector2(30, 20));
        viewModel.IsObjectEraseRunning.Should().BeTrue();

        viewModel.ToggleCropModeCommand.Execute(null);

        requestToken.IsCancellationRequested.Should().BeTrue();
        viewModel.IsObjectEraseRunning.Should().BeFalse();
        viewModel.IsObjectEraseModeActive.Should().BeFalse();

        pendingResult.SetResult(ObjectEraseResult.Success(new ImageFile("stale.png")));
        await erase;

        viewModel.ImageFile!.FilePath.Should().Be("original.png");
        viewModel.HasUndoStack.Should().BeFalse();
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageObjectEraseService objectEraseService,
        IAiFeatureConsentService? consentService = null,
        IAiFeatureConsentDialogService? consentDialogService = null)
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(x => x.GetImageFileSize(It.IsAny<ImageFile>()))
            .Returns(new Size(100, 50));

        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(x => x.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());

        ILocalizationService localization = Mock.Of<ILocalizationService>();
        IAppNotificationService notifications = Mock.Of<IAppNotificationService>();

        return new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            Mock.Of<IImageCanvasExporter>(),
            Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == false),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            Mock.Of<IClipboardService>(),
            new ColorPickerToolViewModel(Mock.Of<IClipboardService>(), localization, notifications),
            new ChromaKeyToolViewModel(
                Mock.Of<IChromaKeyAccessService>(x => x.IsChromaKeyEnabled == false),
                Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(Mock.Of<IClipboardService>(), localization, notifications),
            aiFeatureConsentService: consentService ?? Mock.Of<IAiFeatureConsentService>(x =>
                x.GetConsentState(AiFeatureId.ImageObjectErase) == AiFeatureConsentState.Granted),
            aiFeatureConsentDialogService: consentDialogService ?? Mock.Of<IAiFeatureConsentDialogService>(),
            imageObjectEraseService: objectEraseService,
            imageObjectEraseFeatureAvailability: Mock.Of<IImageObjectEraseFeatureAvailability>(x =>
                x.IsImageObjectEraseEnabled == true));
    }
}
