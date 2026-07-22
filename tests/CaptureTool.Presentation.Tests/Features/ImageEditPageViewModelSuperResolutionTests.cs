using CaptureTool.Application.Abstractions.Cancellation;
using CaptureTool.Application.Abstractions.Clipboard;
using CaptureTool.Application.Abstractions.Edit.External;
using CaptureTool.Application.Abstractions.Edit.Image;
using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Edit.Image.Rendering;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Share;
using CaptureTool.Application.Abstractions.Storage;
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
public sealed class ImageEditPageViewModelSuperResolutionTests
{
    [TestMethod]
    public async Task LoadAsync_WhenSuperResolutionFeatureDisabled_ShouldHideAndDisableToggle()
    {
        var service = new Mock<IImageSuperResolutionService>(MockBehavior.Strict);
        var featureAvailability = new Mock<IImageSuperResolutionFeatureAvailability>();
        featureAvailability
            .Setup(x => x.IsImageSuperResolutionEnabled)
            .Returns(false);

        ImageEditPageViewModel viewModel = CreateViewModel(
            service: service.Object,
            featureAvailability: featureAvailability.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.IsSuperResolutionFeatureEnabled.Should().BeFalse();
        viewModel.IsSuperResolutionAvailable.Should().BeFalse();
        viewModel.CanToggleSuperResolution.Should().BeFalse();
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task LoadAsync_WhenSuperResolutionUnsupported_ShouldDisableToggle()
    {
        var service = new Mock<IImageSuperResolutionService>();
        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.NotSupported);

        ImageEditPageViewModel viewModel = CreateViewModel(service: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);

        viewModel.IsSuperResolutionAvailable.Should().BeFalse();
        viewModel.CanToggleSuperResolution.Should().BeFalse();
    }

    [TestMethod]
    public async Task ToggleSuperResolutionCommand_ShouldGenerateOnce_AndReuseCachedImage()
    {
        var service = new Mock<IImageSuperResolutionService>();
        var original = new ImageFile("original.png");
        var generated = new ImageFile("super.png");

        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.Ready);
        service
            .Setup(x => x.GenerateAsync(
                It.Is<ImageSuperResolutionRequest>(request =>
                    request.SourceImage == original &&
                    request.SourceSize == new Size(100, 50) &&
                    request.ScaleFactor == 2.0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageSuperResolutionResult.Success(generated, new Size(200, 100)));

        ImageEditPageViewModel viewModel = CreateViewModel(service: service.Object);

        await viewModel.LoadAsync(original, CancellationToken.None);
        int canvasResourceReloads = 0;
        viewModel.ReloadCanvasResourcesRequested += (_, _) => canvasResourceReloads++;

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        ImageDrawable imageDrawable = viewModel.Drawables.OfType<ImageDrawable>().Single();
        viewModel.IsSuperResolutionActive.Should().BeTrue();
        viewModel.ImageFile.Should().Be(generated);
        viewModel.ImageSize.Should().Be(new Size(200, 100));
        viewModel.CropRect.Should().Be(new Rectangle(0, 0, 200, 100));
        viewModel.HasUnsavedChanges.Should().BeTrue();
        imageDrawable.File.Should().Be(generated);
        imageDrawable.ImageSize.Should().Be(new Size(200, 100));
        canvasResourceReloads.Should().Be(1);

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        viewModel.IsSuperResolutionActive.Should().BeFalse();
        viewModel.ImageFile.Should().Be(original);
        viewModel.ImageSize.Should().Be(new Size(100, 50));
        viewModel.CropRect.Should().Be(new Rectangle(0, 0, 100, 50));
        viewModel.HasUnsavedChanges.Should().BeFalse();
        imageDrawable.File.Should().Be(original);
        imageDrawable.ImageSize.Should().Be(new Size(100, 50));
        canvasResourceReloads.Should().Be(2);

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        viewModel.IsSuperResolutionActive.Should().BeTrue();
        viewModel.ImageFile.Should().Be(generated);
        imageDrawable.File.Should().Be(generated);
        canvasResourceReloads.Should().Be(3);
        service.Verify(x => x.GenerateAsync(It.IsAny<ImageSuperResolutionRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task ToggleSuperResolutionCommand_ShouldScaleDrawables_WhenSwitchingVariants()
    {
        var service = new Mock<IImageSuperResolutionService>();
        var shape = new RectangleDrawable(new Vector2(10, 5), new Size(20, 10), Color.Red, Color.Transparent, 2);

        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.Ready);
        service
            .Setup(x => x.GenerateAsync(It.IsAny<ImageSuperResolutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageSuperResolutionResult.Success(new ImageFile("super.png"), new Size(200, 100)));

        ImageEditPageViewModel viewModel = CreateViewModel(service: service.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        viewModel.AddDrawable(shape);

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        shape.Offset.Should().Be(new Vector2(20, 10));
        shape.Size.Should().Be(new Size(40, 20));
        shape.StrokeWidth.Should().Be(4);

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        shape.Offset.Should().Be(new Vector2(10, 5));
        shape.Size.Should().Be(new Size(20, 10));
        shape.StrokeWidth.Should().Be(2);
    }

    [TestMethod]
    public async Task ToggleSuperResolutionCommand_WhenPreparationConsentIsDenied_ShouldNotGenerate()
    {
        var service = new Mock<IImageSuperResolutionService>();
        var consent = new Mock<IImageSuperResolutionPreparationConsentService>();

        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.PreparationNeeded);
        consent
            .Setup(x => x.ConfirmPreparationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ImageEditPageViewModel viewModel = CreateViewModel(service: service.Object, consent: consent.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        viewModel.IsSuperResolutionActive.Should().BeFalse();
        viewModel.SuperResolutionStatusMessage.Should().BeEmpty();
        service.Verify(x => x.EnsureReadyAsync(It.IsAny<CancellationToken>()), Times.Never);
        service.Verify(x => x.GenerateAsync(It.IsAny<ImageSuperResolutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ToggleSuperResolutionCommand_WhenGenerationFails_ShouldRestoreOriginalAndShowMessage()
    {
        var service = new Mock<IImageSuperResolutionService>();
        var notifications = new Mock<IAppNotificationService>();
        var original = new ImageFile("original.png");

        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.Ready);
        service
            .Setup(x => x.GenerateAsync(It.IsAny<ImageSuperResolutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageSuperResolutionResult.Failed("No model today."));

        ImageEditPageViewModel viewModel = CreateViewModel(
            service: service.Object,
            notifications: notifications.Object);

        await viewModel.LoadAsync(original, CancellationToken.None);
        List<string?> changedProperties = [];
        viewModel.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        viewModel.IsSuperResolutionActive.Should().BeFalse();
        viewModel.ImageFile.Should().Be(original);
        viewModel.ImageSize.Should().Be(new Size(100, 50));
        viewModel.SuperResolutionStatusMessage.Should().Be("No model today.");
        changedProperties.Should().Contain(nameof(ImageEditPageViewModel.IsSuperResolutionActive));
        notifications.Verify(x => x.ShowError("No model today."), Times.Once);
    }

    [TestMethod]
    public async Task ToggleSuperResolutionCommand_WhenGenerationIsTooLarge_ShouldShowLocalizedMessage()
    {
        var service = new Mock<IImageSuperResolutionService>();
        var notifications = new Mock<IAppNotificationService>();

        service
            .Setup(x => x.GetReadyState())
            .Returns(ImageSuperResolutionReadyState.Ready);
        service
            .Setup(x => x.GenerateAsync(It.IsAny<ImageSuperResolutionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImageSuperResolutionResult.TooLarge());

        ImageEditPageViewModel viewModel = CreateViewModel(
            service: service.Object,
            notifications: notifications.Object);

        await viewModel.LoadAsync(new ImageFile("original.png"), CancellationToken.None);
        await viewModel.ToggleSuperResolutionCommand.ExecuteAsync(null);

        viewModel.SuperResolutionStatusMessage.Should().Be("This image is too large for Super Resolution.");
        notifications.Verify(x => x.ShowError("This image is too large for Super Resolution."), Times.Once);
    }

    private static ImageEditPageViewModel CreateViewModel(
        IImageSuperResolutionService? service = null,
        IImageSuperResolutionFeatureAvailability? featureAvailability = null,
        IImageSuperResolutionPreparationConsentService? consent = null,
        ILocalizationService? localizationService = null,
        IAppNotificationService? notifications = null)
    {
        var imageMetadata = new Mock<IImageMetadataService>();
        imageMetadata
            .Setup(x => x.GetImageFileSize(It.IsAny<ImageFile>()))
            .Returns(new Size(100, 50));

        var cancellationService = new Mock<ICancellationService>();
        cancellationService
            .Setup(x => x.GetLinkedCancellationTokenSource(It.IsAny<CancellationToken>()))
            .Returns(() => new CancellationTokenSource());

        var chromaKeyAccess = new Mock<IChromaKeyAccessService>();
        chromaKeyAccess
            .Setup(x => x.IsChromaKeyEnabled)
            .Returns(false);

        IAppNotificationService notificationService = notifications ?? Mock.Of<IAppNotificationService>();

        return new ImageEditPageViewModel(
            localizationService ?? CreateLocalizationService(),
            cancellationService.Object,
            Mock.Of<IImageCanvasPrinter>(),
            Mock.Of<IImageCanvasExporter>(),
            Mock.Of<IFilePickerService>(),
            imageMetadata.Object,
            service ?? Mock.Of<IImageSuperResolutionService>(),
            featureAvailability ?? Mock.Of<IImageSuperResolutionFeatureAvailability>(x => x.IsImageSuperResolutionEnabled == true),
            consent ?? Mock.Of<IImageSuperResolutionPreparationConsentService>(),
            Mock.Of<IShareService>(),
            Mock.Of<IOpenExternalEditorUseCase>(),
            Mock.Of<IStorageService>(),
            Mock.Of<ISettingsService>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<ILogService>(),
            notificationService,
            Mock.Of<IClipboardService>(),
            new ColorPickerToolViewModel(
                Mock.Of<IClipboardService>(),
                localizationService ?? CreateLocalizationService(),
                notificationService),
            new ChromaKeyToolViewModel(chromaKeyAccess.Object, Mock.Of<IChromaKeyService>()),
            new ShapeToolViewModel(),
            new TextToolViewModel(),
            new TextExtractionToolViewModel(
                Mock.Of<IClipboardService>(),
                localizationService ?? CreateLocalizationService(),
                notificationService));
    }

    private static ILocalizationService CreateLocalizationService()
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(x => x.GetString(It.IsAny<string>()))
            .Returns((string resourceKey) => resourceKey switch
            {
                "ImageSuperResolutionStatus_NotSupported" => "Super Resolution is not supported on this PC.",
                "ImageSuperResolutionStatus_Disabled" => "Super Resolution is disabled on this PC.",
                "ImageSuperResolutionStatus_NotAvailable" => "Super Resolution is not available.",
                "ImageSuperResolutionStatus_PreparationFailed" => "Super Resolution could not be prepared.",
                "ImageSuperResolutionStatus_NotReady" => "Super Resolution is not ready.",
                "ImageSuperResolutionStatus_TooLarge" => "This image is too large for Super Resolution.",
                "ImageSuperResolutionStatus_Failed" => "Super Resolution failed.",
                _ => resourceKey
            });

        return localization.Object;
    }
}
