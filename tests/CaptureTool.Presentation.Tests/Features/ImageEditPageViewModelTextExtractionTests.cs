using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Domain.Ai;
using CaptureTool.Domain.Edit.Drawable;
using CaptureTool.Domain.FileSystem;
using CaptureTool.Presentation.Features.ImageEdit;
using CaptureTool.Presentation.Notifications;
using FluentAssertions;
using Moq;
using System.Drawing;
using System.Numerics;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class ImageEditPageViewModelTextExtractionTests
{
    [TestMethod]
    public async Task LoadAsync_WhenTextExtractionFeatureDisabled_ShouldHideAndDisableToggle()
    {
        var textExtraction = new Mock<ITextExtractionService>(MockBehavior.Strict);
        ImageEditPageViewModel viewModel = CreateViewModel(
            textExtractionService: textExtraction.Object,
            textExtractionFeatureAvailability: Mock.Of<ITextExtractionFeatureAvailability>(x => x.IsTextExtractionEnabled == false));

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.IsTextExtractionFeatureEnabled.Should().BeFalse();
        viewModel.IsTextExtractionAvailable.Should().BeFalse();
        viewModel.CanToggleTextExtraction.Should().BeFalse();
        textExtraction.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenFirstUseConsentDenied_ShouldPersistDenialAndNotExtract()
    {
        AiFeatureConsentState consentState = AiFeatureConsentState.Unknown;
        var consent = new Mock<IAiFeatureConsentService>();
        var dialog = new Mock<IAiFeatureConsentDialogService>();
        var textExtraction = new Mock<ITextExtractionService>();

        consent
            .Setup(service => service.GetConsentState(AiFeatureId.TextExtraction))
            .Returns(() => consentState);
        consent
            .Setup(service => service.SetConsentAsync(AiFeatureId.TextExtraction, false, It.IsAny<CancellationToken>()))
            .Callback(() => consentState = AiFeatureConsentState.Denied)
            .Returns(Task.CompletedTask);
        consent
            .Setup(service => service.GetConsentState(AiFeatureId.ImageSuperResolution))
            .Returns(AiFeatureConsentState.Granted);
        dialog
            .Setup(service => service.RequestConsentAsync(AiFeatureId.TextExtraction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);

        ImageEditPageViewModel viewModel = CreateViewModel(
            aiFeatureConsentService: consent.Object,
            aiFeatureConsentDialogService: dialog.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.CanToggleTextExtraction.Should().BeTrue();
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.CanToggleTextExtraction.Should().BeFalse();
        consent.Verify(service => service.SetConsentAsync(AiFeatureId.TextExtraction, false, It.IsAny<CancellationToken>()), Times.Once);
        textExtraction.Verify(service => service.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenConsented_ShouldRenderCurrentImageAndShowRegions()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        var storage = new Mock<IStorageService>();
        var sourceImage = new ImageFile("original.png");
        string tempFolder = Path.Combine(Path.GetTempPath(), "CaptureToolTests", Guid.NewGuid().ToString());

        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(tempFolder);
        storage.Setup(service => service.GetTemporaryFileName()).Returns("ocr.tmp");
        exporter
            .Setup(service => service.SaveImageAsync(It.IsAny<string>(), It.IsAny<IDrawable[]>(), It.IsAny<ImageCanvasRenderOptions>()))
            .Returns(Task.CompletedTask);
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(
                It.Is<TextExtractionRequest>(request =>
                    request.SourceImage.FilePath == Path.Combine(tempFolder, "ocr.text-extraction.png") &&
                    request.SourceSize == new Size(100, 50)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TextExtractionResult.Success(new RecognizedTextDocument(
                "hello",
                new Size(100, 50),
                [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))])));

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            storageService: storage.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(sourceImage, CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        viewModel.IsTextExtractionModeActive.Should().BeTrue();
        viewModel.TextExtractionRegions.Should().ContainSingle();
        viewModel.TextExtractionRegions[0].Bounds.Should().Be(new RectangleF(8, 8, 24, 9));
        exporter.Verify(service => service.SaveImageAsync(
            Path.Combine(tempFolder, "ocr.text-extraction.png"),
            It.IsAny<IDrawable[]>(),
            It.Is<ImageCanvasRenderOptions>(options =>
                options.CanvasSize == new Size(100, 50) &&
                options.CropRect == new Rectangle(0, 0, 100, 50))),
            Times.Once);
    }

    [TestMethod]
    public async Task ToggleTextExtractionMode_WhenEditRevisionChanged_ShouldRunAgainWhenReopened()
    {
        var exporter = new Mock<IImageCanvasExporter>();
        var textExtraction = new Mock<ITextExtractionService>();
        var storage = new Mock<IStorageService>();
        int extractionCount = 0;

        storage.Setup(service => service.GetApplicationTemporaryFolderPath()).Returns(Path.GetTempPath());
        storage.Setup(service => service.GetTemporaryFileName()).Returns("ocr.tmp");
        exporter
            .Setup(service => service.SaveImageAsync(It.IsAny<string>(), It.IsAny<IDrawable[]>(), It.IsAny<ImageCanvasRenderOptions>()))
            .Returns(Task.CompletedTask);
        textExtraction
            .Setup(service => service.GetReadyState())
            .Returns(TextExtractionReadyState.Ready);
        textExtraction
            .Setup(service => service.ExtractAsync(It.IsAny<TextExtractionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                extractionCount++;
                return TextExtractionResult.Success(new RecognizedTextDocument(
                    "hello",
                    new Size(100, 50),
                    [new RecognizedTextRegion("hello", new RectangleF(10, 10, 20, 5))]));
            });

        ImageEditPageViewModel viewModel = CreateViewModel(
            imageCanvasExporter: exporter.Object,
            storageService: storage.Object,
            textExtractionService: textExtraction.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);
        extractionCount.Should().Be(1);

        viewModel.AddDrawable(new RectangleDrawable(
            new Vector2(1, 1),
            new Size(10, 10),
            Color.Red,
            Color.Transparent,
            1));

        viewModel.IsTextExtractionModeActive.Should().BeFalse();
        viewModel.TextExtractionRegions.Should().BeEmpty();

        await viewModel.ToggleTextExtractionModeCommand.ExecuteAsync(null);

        extractionCount.Should().Be(2);
        viewModel.IsTextExtractionModeActive.Should().BeTrue();
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageCanvasExporter? imageCanvasExporter = null,
        IStorageService? storageService = null,
        ITextExtractionService? textExtractionService = null,
        ITextExtractionFeatureAvailability? textExtractionFeatureAvailability = null,
        IAiFeatureConsentService? aiFeatureConsentService = null,
        IAiFeatureConsentDialogService? aiFeatureConsentDialogService = null)
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(service => service.GetImageFileSize(It.IsAny<ImageFile>()))
            .Returns(new Size(100, 50));

        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(service => service.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());

        IAppNotificationService notifications = Mock.Of<IAppNotificationService>();
        ILocalizationService localization = Mock.Of<ILocalizationService>();

        return new ImageEditPageViewModel(
            localization,
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            imageCanvasExporter ?? Mock.Of<IImageCanvasExporter>(),
            Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            Mock.Of<IImageSuperResolutionService>(),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == false),
            Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            storageService ?? Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notifications,
            new ColorPickerToolViewModel(
                Mock.Of<IClipboardService>(),
                localization,
                notifications),
            new ChromaKeyToolViewModel(
                Mock.Of<IChromaKeyAccessService>(service => service.IsChromaKeyEnabled == false),
                Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            aiFeatureConsentService ?? Mock.Of<IAiFeatureConsentService>(
                service => service.GetConsentState(AiFeatureId.TextExtraction) == AiFeatureConsentState.Granted &&
                    service.GetConsentState(AiFeatureId.ImageSuperResolution) == AiFeatureConsentState.Granted),
            aiFeatureConsentDialogService ?? Mock.Of<IAiFeatureConsentDialogService>(),
            textExtractionService ?? Mock.Of<ITextExtractionService>(service => service.GetReadyState() == TextExtractionReadyState.Ready),
            textExtractionFeatureAvailability ?? Mock.Of<ITextExtractionFeatureAvailability>(service => service.IsTextExtractionEnabled == true));
    }
}

