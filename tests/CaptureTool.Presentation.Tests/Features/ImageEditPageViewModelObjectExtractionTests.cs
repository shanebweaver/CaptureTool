using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
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
public sealed class ImageEditPageViewModelObjectExtractionTests
{
    [TestMethod]
    public async Task ToggleMode_WhenConsentRefused_ShouldRemainInactive()
    {
        var consent = new Mock<IAiFeatureConsentService>();
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        var service = new Mock<IImageForegroundExtractionService>();
        consent
            .Setup(x => x.GetConsentState(AiFeatureId.ImageObjectExtraction))
            .Returns(AiFeatureConsentState.Denied);
        consent
            .Setup(x => x.SetConsentAsync(AiFeatureId.ImageObjectExtraction, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        dialog
            .Setup(x => x.RequestConsentAsync(AiFeatureId.ImageObjectExtraction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        service.Setup(x => x.GetReadyState()).Returns(ForegroundExtractionReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object, consent.Object, dialog.Object);
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        await viewModel.ToggleObjectExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsObjectExtractionModeActive.Should().BeFalse();
        viewModel.CanToggleObjectExtraction.Should().BeTrue();
        service.Verify(
            x => x.ExtractAsync(It.IsAny<ForegroundExtractionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [TestMethod]
    public async Task ClickingImage_ShouldApplyUndoableObjectExtractionResult()
    {
        var service = new Mock<IImageForegroundExtractionService>();
        service.Setup(x => x.GetReadyState()).Returns(ForegroundExtractionReadyState.Ready);
        service
            .Setup(x => x.ExtractAsync(
                It.Is<ForegroundExtractionRequest>(request =>
                    request.SourceImage.FilePath == "original.png" &&
                    request.SourceSize == new Size(100, 50) &&
                    request.ForegroundPoint == new Point(30, 20)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ForegroundExtractionResult.Success(new ImageFile("object.png")));

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object);
        int reloadCount = 0;
        viewModel.ReloadCanvasResourcesRequested += (_, _) => reloadCount++;
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleObjectExtractionModeCommand.ExecuteAsync(null);

        await viewModel.OnObjectExtractionRequestedAsync(new Vector2(30, 20));

        viewModel.ImageFile!.FilePath.Should().Be("object.png");
        viewModel.HasUndoStack.Should().BeTrue();
        viewModel.IsObjectExtractionModeActive.Should().BeTrue();
        reloadCount.Should().Be(1);

        viewModel.UndoCommand.Execute(null);
        viewModel.ImageFile!.FilePath.Should().Be("original.png");
        viewModel.HasRedoStack.Should().BeTrue();
        viewModel.IsObjectExtractionModeActive.Should().BeFalse();

        viewModel.RedoCommand.Execute(null);
        viewModel.ImageFile!.FilePath.Should().Be("object.png");
        reloadCount.Should().Be(3);
    }

    [TestMethod]
    public async Task ChangingModes_ShouldCancelPendingExtractionAndIgnoreItsResult()
    {
        var service = new Mock<IImageForegroundExtractionService>();
        var pendingResult = new TaskCompletionSource<ForegroundExtractionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken requestToken = default;
        service.Setup(x => x.GetReadyState()).Returns(ForegroundExtractionReadyState.Ready);
        service
            .Setup(x => x.ExtractAsync(It.IsAny<ForegroundExtractionRequest>(), It.IsAny<CancellationToken>()))
            .Returns((ForegroundExtractionRequest _, CancellationToken token) =>
            {
                requestToken = token;
                return pendingResult.Task;
            });

        ImageEditPageViewModel viewModel = CreateViewModel(service.Object);
        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleObjectExtractionModeCommand.ExecuteAsync(null);

        Task extraction = viewModel.OnObjectExtractionRequestedAsync(new Vector2(30, 20));
        viewModel.IsObjectExtractionRunning.Should().BeTrue();

        viewModel.ToggleCropModeCommand.Execute(null);

        requestToken.IsCancellationRequested.Should().BeTrue();
        viewModel.IsObjectExtractionRunning.Should().BeFalse();
        viewModel.IsObjectExtractionModeActive.Should().BeFalse();

        pendingResult.SetResult(ForegroundExtractionResult.Success(new ImageFile("stale.png")));
        await extraction;

        viewModel.ImageFile!.FilePath.Should().Be("original.png");
        viewModel.HasUndoStack.Should().BeFalse();
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageForegroundExtractionService objectExtractionService,
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
                x.GetConsentState(AiFeatureId.ImageObjectExtraction) == AiFeatureConsentState.Granted),
            aiFeatureConsentDialogService: consentDialogService ?? Mock.Of<IAiFeatureConsentDialogService>(),
            imageForegroundExtractionService: objectExtractionService,
            imageObjectExtractionFeatureAvailability: Mock.Of<IImageObjectExtractionFeatureAvailability>(x =>
                x.IsImageObjectExtractionEnabled == true));
    }
}
