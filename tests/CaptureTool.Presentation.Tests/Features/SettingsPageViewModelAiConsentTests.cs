using CaptureTool.Application.Abstractions.Ai;
using CaptureTool.Application.Abstractions.Analysis.Consent;
using CaptureTool.Application.Abstractions.Analysis.Models;
using CaptureTool.Application.Abstractions.Edit.Image.Description;
using CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;
using CaptureTool.Application.Abstractions.Edit.Image.ObjectExtraction;
using CaptureTool.Application.Abstractions.Edit.Image.SuperResolution;
using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Edit.Video.SuperResolution;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Metrics;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Ai;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Features.Settings;
using FluentAssertions;
using Moq;

namespace CaptureTool.Presentation.Tests.Features;

[TestClass]
public sealed class SettingsPageViewModelAiConsentTests
{
    [TestMethod]
    public async Task LoadAsync_WithTelemetryConsent_ShouldEnableOptionalUsageData()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentValue: TelemetryConsentSettingValues.Granted);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.OptionalUsageDataEnabled.Should().BeTrue();
    }

    [TestMethod]
    public async Task UpdateOptionalUsageDataEnabledCommand_ShouldUpdateConsentGate()
    {
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentService: telemetryConsent.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateOptionalUsageDataEnabledCommand.ExecuteAsync(true);

        viewModel.OptionalUsageDataEnabled.Should().BeTrue();
        telemetryConsent.Verify(
            service => service.SetState(TelemetryConsentState.Granted),
            Times.Once);
    }

    [TestMethod]
    public async Task UpdateOptionalUsageDataEnabledCommand_WhenPersistenceFails_RevertsConsentGate()
    {
        var telemetryConsent = new Mock<ITelemetryConsentService>();
        SettingsPageViewModel viewModel = CreateViewModel(
            telemetryConsentService: telemetryConsent.Object,
            settingsMutationStatus: SettingsMutationStatus.PersistenceFailed);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateOptionalUsageDataEnabledCommand.ExecuteAsync(true);

        viewModel.OptionalUsageDataEnabled.Should().BeFalse();
        telemetryConsent.Verify(
            service => service.SetState(It.IsAny<TelemetryConsentState>()),
            Times.Never);
    }

    [TestMethod]
    public async Task UpdateImageCaptureAutoSaveCommand_WhenPersistenceFails_RevertsOptimisticValue()
    {
        var updateAction = new Mock<IUpdateImageAutoSaveUseCase>();
        updateAction
            .Setup(action => action.ExecuteAsync(
                new UpdateImageAutoSaveRequest(false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<UpdateImageAutoSaveResponse>.Success(
                new UpdateImageAutoSaveResponse(false)));
        SettingsPageViewModel viewModel = CreateViewModel(
            updateImageAutoSaveAction: updateAction.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateImageCaptureAutoSaveCommand.ExecuteAsync(false);

        viewModel.ImageCaptureAutoSave.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_WhenAiConsentSettingsFeatureDisabled_ShouldHideSectionAndRows()
    {
        SettingsPageViewModel viewModel = CreateViewModel(isAiConsentSettingsEnabled: false);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeFalse();
        viewModel.AiFeatureConsents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlySuperResolutionFeatureEnabled_ShouldShowSuperResolutionConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: true,
            isTextExtractionEnabled: false);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.ImageSuperResolution &&
            consent.DisplayName == "Localized image super resolution" &&
            consent.Description.Contains("editing tool"));
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyTextExtractionFeatureEnabled_ShouldShowTextExtractionConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.TextExtraction &&
            consent.DisplayName == "Localized text extraction");
    }

    [TestMethod]
    public async Task UpdateAiFeatureConsentAsync_WhenPersistenceFails_KeepsCommittedConsent()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: true,
            aiConsentSaveSucceeded: false);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.UpdateAiFeatureConsentAsync(AiFeatureId.TextExtraction, false);

        viewModel.AiFeatureConsents.Single().IsConsented.Should().BeTrue();
    }

    [TestMethod]
    public async Task LoadAsync_WhenNoChildAiFeaturesEnabled_ShouldHideSectionAndRows()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeFalse();
        viewModel.AiFeatureConsents.Should().BeEmpty();
    }

    [TestMethod]
    public async Task RestoreDefaultSettingsCommand_WhenRestoreFails_ShouldKeepDisplayedTheme()
    {
        var restoreDefaults = new Mock<IRestoreDefaultsUseCase>();
        restoreDefaults
            .Setup(service => service.ExecuteAsync(It.IsAny<RestoreDefaultsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<RestoreDefaultsResponse>.Failure());
        SettingsPageViewModel viewModel = CreateViewModel(restoreDefaultsUseCase: restoreDefaults.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);
        int selectedThemeIndex = viewModel.SelectedAppThemeIndex;

        await viewModel.RestoreDefaultSettingsCommand.ExecuteAsync(null);

        viewModel.SelectedAppThemeIndex.Should().Be(selectedThemeIndex);
    }

    [TestMethod]
    public async Task ClearTemporaryFilesCommand_ShouldReportReclaimedAndActiveWorkingFiles()
    {
        var clear = new Mock<IClearTempFilesUseCase>();
        clear.Setup(service => service.ExecuteAsync(
                It.IsAny<ClearTempFilesRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(UseCaseResponse<ClearTempFilesResponse>.Success(
                new ClearTempFilesResponse(2, 4096, 1, 1024, 0)));
        SettingsPageViewModel viewModel = CreateViewModel(clearTempFilesUseCase: clear.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.ClearTemporaryFilesCommand.ExecuteAsync(null);

        viewModel.HasTemporaryFilesStatus.Should().BeTrue();
        viewModel.TemporaryFilesStatusText.Should().Contain("4.0 KB");
        viewModel.TemporaryFilesStatusText.Should().Contain("2 temporary item");
        viewModel.TemporaryFilesStatusText.Should().Contain("1 active item");
    }

    [TestMethod]
    public async Task LoadAsync_WithModelStorage_ShouldReportDownloadedModelSize()
    {
        var modelStorage = new Mock<IAiModelStorageService>();
        modelStorage
            .Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelStorageSnapshot(2L * 1024 * 1024 * 1024));
        SettingsPageViewModel viewModel = CreateViewModel(
            aiModelStorageService: modelStorage.Object,
            confirmationService: Mock.Of<ICaptureAnalysisSettingsConfirmationDialogService>());

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiModelStorageVisible.Should().BeTrue();
        viewModel.IsAiModelStorageMeasured.Should().BeTrue();
        viewModel.DownloadedAiModelByteCount.Should().Be(2L * 1024 * 1024 * 1024);
        viewModel.AiModelStorageSummaryText.Should().Contain("2.0 GB");
        viewModel.CanRemoveDownloadedAiModels.Should().BeTrue();
    }

    [TestMethod]
    public async Task RemoveDownloadedAiModelsCommand_ShouldConfirmAndPreserveReportedRemainder()
    {
        var modelStorage = new Mock<IAiModelStorageService>();
        modelStorage
            .Setup(service => service.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelStorageSnapshot(4096));
        modelStorage
            .Setup(service => service.RemoveDownloadedModelsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiModelStorageRemovalResult(2, 4096, 0, 0));
        var confirmation = new Mock<ICaptureAnalysisSettingsConfirmationDialogService>();
        confirmation
            .Setup(service => service.ConfirmAsync(
                It.Is<CaptureAnalysisSettingsConfirmationRequest>(request =>
                    request.Action == CaptureAnalysisSettingsAction.RemoveDownloadedModels),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptureAnalysisConfirmationDecision.Confirmed);
        SettingsPageViewModel viewModel = CreateViewModel(
            aiModelStorageService: modelStorage.Object,
            confirmationService: confirmation.Object);
        await viewModel.LoadAsync(TestContext.CancellationToken);

        await viewModel.RemoveDownloadedAiModelsCommand.ExecuteAsync(null);

        viewModel.DownloadedAiModelByteCount.Should().Be(0);
        viewModel.AiModelStorageStatusText.Should().Contain("2 downloaded model");
        viewModel.AiModelStorageStatusText.Should().Contain("4.0 KB");
        modelStorage.Verify(
            service => service.RemoveDownloadedModelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyImageDescriptionFeatureEnabled_ShouldShowImageDescriptionConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false,
            isImageDescriptionEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.ImageDescription &&
            consent.DisplayName == "Localized image description");
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyForegroundExtractionFeatureEnabled_ShouldShowBackgroundRemovalConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false,
            isImageDescriptionEnabled: false,
            isImageForegroundExtractionEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.ImageForegroundExtraction &&
            consent.DisplayName == "Localized background removal");
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyObjectEraseFeatureEnabled_ShouldShowObjectEraseConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false,
            isImageDescriptionEnabled: false,
            isImageForegroundExtractionEnabled: false,
            isImageObjectEraseEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.ImageObjectErase &&
            consent.DisplayName == "Localized object erase");
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyObjectExtractionFeatureEnabled_ShouldShowObjectExtractionConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false,
            isImageDescriptionEnabled: false,
            isImageForegroundExtractionEnabled: false,
            isImageObjectEraseEnabled: false,
            isImageObjectExtractionEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.ImageObjectExtraction &&
            consent.DisplayName == "Localized object extraction");
    }

    [TestMethod]
    public async Task LoadAsync_WhenOnlyVideoSuperResolutionFeatureEnabled_ShouldShowVideoSuperResolutionConsentOnly()
    {
        SettingsPageViewModel viewModel = CreateViewModel(
            isImageSuperResolutionEnabled: false,
            isTextExtractionEnabled: false,
            isImageDescriptionEnabled: false,
            isImageForegroundExtractionEnabled: false,
            isImageObjectEraseEnabled: false,
            isImageObjectExtractionEnabled: false,
            isVideoSuperResolutionEnabled: true);

        await viewModel.LoadAsync(TestContext.CancellationToken);

        viewModel.IsAiConsentSettingsVisible.Should().BeTrue();
        viewModel.AiFeatureConsents.Should().ContainSingle(consent =>
            consent.FeatureId == AiFeatureId.VideoSuperResolution &&
            consent.DisplayName == "Localized video super resolution");
    }

    private static SettingsPageViewModel CreateViewModel(
        bool isAiConsentSettingsEnabled = true,
        bool isImageSuperResolutionEnabled = true,
        bool isTextExtractionEnabled = true,
        bool isImageDescriptionEnabled = false,
        bool isImageForegroundExtractionEnabled = false,
        bool isImageObjectEraseEnabled = false,
        bool isImageObjectExtractionEnabled = false,
        bool isVideoSuperResolutionEnabled = false,
        string telemetryConsentValue = TelemetryConsentSettingValues.Unknown,
        ITelemetryConsentService? telemetryConsentService = null,
        SettingsMutationStatus settingsMutationStatus = SettingsMutationStatus.Saved,
        IUpdateImageAutoSaveUseCase? updateImageAutoSaveAction = null,
        bool aiConsentSaveSucceeded = true,
        IRestoreDefaultsUseCase? restoreDefaultsUseCase = null,
        IClearTempFilesUseCase? clearTempFilesUseCase = null,
        IAiModelStorageService? aiModelStorageService = null,
        ICaptureAnalysisSettingsConfirmationDialogService? confirmationService = null)
    {
        var localization = new Mock<ILocalizationService>();
        localization
            .Setup(service => service.SupportedLanguages)
            .Returns([]);
        localization
            .Setup(service => service.GetString(It.IsAny<string>()))
            .Returns<string>(resourceKey => resourceKey switch
            {
                "Settings_AiConsent_TextExtractionDisplayName" => "Localized text extraction",
                "Settings_AiConsent_ImageSuperResolutionDisplayName" => "Localized image super resolution",
                "Settings_AiConsent_ImageDescriptionDisplayName" => "Localized image description",
                "Settings_AiConsent_ImageForegroundExtractionDisplayName" => "Localized background removal",
                "Settings_AiConsent_ImageObjectEraseDisplayName" => "Localized object erase",
                "Settings_AiConsent_ImageObjectExtractionDisplayName" => "Localized object extraction",
                "Settings_AiConsent_VideoSuperResolutionDisplayName" => "Localized video super resolution",
                "Settings_AiModelStorage_Size" => "Downloaded on-device models use {0}.",
                "Settings_AiModelStorage_RemoveSucceeded" =>
                    "Removed {0} downloaded model(s) and freed {1}. Capture files and analyzed metadata were kept.",
                _ => resourceKey
            });

        var settings = new Mock<ISettingsService>();
        settings
            .Setup(service => service.Get(It.IsAny<ISettingDefinitionWithValue<bool>>()))
            .Returns<ISettingDefinitionWithValue<bool>>(definition => definition.Value);
        settings
            .Setup(service => service.Get(It.IsAny<ISettingDefinitionWithValue<string>>()))
            .Returns<ISettingDefinitionWithValue<string>>(definition => definition.Value);
        settings
            .Setup(service => service.Get(CaptureToolSettings.Settings_TelemetryConsent))
            .Returns(telemetryConsentValue);
        settings
            .Setup(service => service.TrySetAndSaveAsync(
                It.IsAny<IStringSettingDefinition>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SettingsMutationResult(settingsMutationStatus));

        var aiFeatureConsentService = new Mock<IAiFeatureConsentService>();
        aiFeatureConsentService
            .Setup(service => service.GetFeatureConsents())
            .Returns([
                new(AiFeatureId.TextExtraction, "Text extraction", AiFeatureConsentState.Granted),
                new(AiFeatureId.ImageSuperResolution, "Super image resolution", AiFeatureConsentState.Granted),
                new(AiFeatureId.ImageDescription, "Image description", AiFeatureConsentState.Granted),
                new(AiFeatureId.ImageForegroundExtraction, "Background removal", AiFeatureConsentState.Granted),
                new(AiFeatureId.ImageObjectErase, "Object erase", AiFeatureConsentState.Granted),
                new(AiFeatureId.ImageObjectExtraction, "Object extraction", AiFeatureConsentState.Granted),
                new(AiFeatureId.VideoSuperResolution, "Video super resolution", AiFeatureConsentState.Granted)
            ]);
        aiFeatureConsentService
            .Setup(service => service.SetConsentAsync(
                It.IsAny<AiFeatureId>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(aiConsentSaveSucceeded);

        var appLanguageViewModelFactory = new Mock<IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>>();
        appLanguageViewModelFactory
            .Setup(factory => factory.Create(It.IsAny<IAppLanguage?>()))
            .Returns<IAppLanguage?>(language => new AppLanguageViewModel(language, localization.Object));

        var appThemeViewModelFactory = new Mock<IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>>();
        appThemeViewModelFactory
            .Setup(factory => factory.Create(It.IsAny<AppTheme>()))
            .Returns<AppTheme>(theme => new AppThemeViewModel(theme, localization.Object));

        var storage = new Mock<IStorageService>();
        storage
            .Setup(service => service.GetSystemDefaultScreenshotsFolderPath())
            .Returns(@"C:\Screenshots");
        storage
            .Setup(service => service.GetSystemDefaultVideosFolderPath())
            .Returns(@"C:\Videos");
        storage
            .Setup(service => service.GetSystemDefaultMusicFolderPath())
            .Returns(@"C:\Music");
        storage
            .Setup(service => service.GetApplicationScratchFolderPath())
            .Returns(@"C:\Temp");

        return new SettingsPageViewModel(
            Mock.Of<ILeaveSettingsPageUseCase>(),
            Mock.Of<IRestartSettingsApplicationUseCase>(),
            Mock.Of<IUpdateImageAutoCopyUseCase>(),
            updateImageAutoSaveAction ?? Mock.Of<IUpdateImageAutoSaveUseCase>(),
            Mock.Of<IUpdateAudioCaptureAutoCopyUseCase>(),
            Mock.Of<IUpdateAudioCaptureAutoSaveUseCase>(),
            Mock.Of<IUpdateAudioCaptureDefaultLocalAudioUseCase>(),
            Mock.Of<IUpdateVideoCaptureAutoCopyUseCase>(),
            Mock.Of<IUpdateVideoCaptureAutoSaveUseCase>(),
            Mock.Of<IUpdateVideoCaptureDefaultLocalAudioUseCase>(),
            Mock.Of<IUpdateCaptureWarnBeforeDiscardUseCase>(),
            Mock.Of<IUpdateEditWarnBeforeDiscardUseCase>(),
            Mock.Of<IUpdateAppLanguageUseCase>(),
            Mock.Of<IUpdateAppThemeUseCase>(),
            Mock.Of<IChangeScreenshotsFolderUseCase>(),
            Mock.Of<IOpenScreenshotsFolderUseCase>(),
            Mock.Of<IChangeAudioFolderUseCase>(),
            Mock.Of<IOpenAudioFolderUseCase>(),
            Mock.Of<IChangeVideosFolderUseCase>(),
            Mock.Of<IOpenVideosFolderUseCase>(),
            Mock.Of<IOpenTempFolderUseCase>(),
            clearTempFilesUseCase ?? Mock.Of<IClearTempFilesUseCase>(),
            restoreDefaultsUseCase ?? Mock.Of<IRestoreDefaultsUseCase>(),
            aiFeatureConsentService.Object,
            Mock.Of<IAiConsentSettingsFeatureAvailability>(service =>
                service.IsAiConsentSettingsEnabled == isAiConsentSettingsEnabled),
            Mock.Of<IImageSuperResolutionFeatureAvailability>(service =>
                service.IsImageSuperResolutionEnabled == isImageSuperResolutionEnabled),
            Mock.Of<ITextExtractionFeatureAvailability>(service =>
                service.IsTextExtractionEnabled == isTextExtractionEnabled),
            localization.Object,
            Mock.Of<IThemeService>(service =>
                service.DefaultTheme == AppTheme.Light &&
                service.StartupTheme == AppTheme.Light &&
                service.CurrentTheme == AppTheme.Light),
            settings.Object,
            Mock.Of<IAppMetricsService>(),
            Mock.Of<IStoreService>(),
            storage.Object,
            appLanguageViewModelFactory.Object,
            appThemeViewModelFactory.Object,
            Mock.Of<IImageDescriptionFeatureAvailability>(service =>
                service.IsImageDescriptionEnabled == isImageDescriptionEnabled),
            Mock.Of<IImageForegroundExtractionFeatureAvailability>(service =>
                service.IsImageForegroundExtractionEnabled == isImageForegroundExtractionEnabled),
            Mock.Of<IImageObjectEraseFeatureAvailability>(service =>
                service.IsImageObjectEraseEnabled == isImageObjectEraseEnabled),
            Mock.Of<IImageObjectExtractionFeatureAvailability>(service =>
                service.IsImageObjectExtractionEnabled == isImageObjectExtractionEnabled),
            Mock.Of<IVideoSuperResolutionFeatureAvailability>(service =>
                service.IsVideoSuperResolutionEnabled == isVideoSuperResolutionEnabled),
            telemetryConsentService,
            captureMemory: null,
            aiModelStorageService,
            confirmationService);
    }

    public TestContext TestContext { get; set; } = null!;
}
