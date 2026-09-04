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
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace CaptureTool.Presentation.Features.Settings;

public sealed partial class SettingsPageViewModel : AsyncLoadableViewModelBase
{
    private readonly ILeaveSettingsPageUseCase _goBackAction;
    private readonly IRestartSettingsApplicationUseCase _restartAppAction;
    private readonly IUpdateImageAutoCopyUseCase _updateImageAutoCopyAction;
    private readonly IUpdateImageAutoSaveUseCase _updateImageAutoSaveAction;
    private readonly IUpdateAudioCaptureAutoCopyUseCase _updateAudioCaptureAutoCopyAction;
    private readonly IUpdateAudioCaptureAutoSaveUseCase _updateAudioCaptureAutoSaveAction;
    private readonly IUpdateAudioCaptureDefaultLocalAudioUseCase _updateAudioCaptureDefaultLocalAudioAction;
    private readonly IUpdateVideoCaptureAutoCopyUseCase _updateVideoCaptureAutoCopyAction;
    private readonly IUpdateVideoCaptureAutoSaveUseCase _updateVideoCaptureAutoSaveAction;
    private readonly IUpdateVideoCaptureDefaultLocalAudioUseCase _updateVideoCaptureDefaultLocalAudioAction;
    private readonly IUpdateCaptureWarnBeforeDiscardUseCase _updateCaptureWarnBeforeDiscardAction;
    private readonly IUpdateEditWarnBeforeDiscardUseCase _updateEditWarnBeforeDiscardAction;
    private readonly IUpdateAppLanguageUseCase _updateAppLanguageAction;
    private readonly IUpdateAppThemeUseCase _updateAppThemeAction;
    private readonly IChangeScreenshotsFolderUseCase _changeScreenshotsFolderAction;
    private readonly IOpenScreenshotsFolderUseCase _openScreenshotsFolderAction;
    private readonly IChangeAudioFolderUseCase _changeAudioFolderAction;
    private readonly IOpenAudioFolderUseCase _openAudioFolderAction;
    private readonly IChangeVideosFolderUseCase _changeVideosFolderAction;
    private readonly IOpenVideosFolderUseCase _openVideosFolderAction;
    private readonly IOpenTempFolderUseCase _openTempFolderAction;
    private readonly IClearTempFilesUseCase _clearTempFilesAction;
    private readonly IRestoreDefaultsUseCase _restoreDefaultsAction;
    private readonly IAiFeatureConsentService _aiFeatureConsentService;
    private readonly IAiConsentSettingsFeatureAvailability _aiConsentSettingsFeatureAvailability;
    private readonly IImageSuperResolutionFeatureAvailability _imageSuperResolutionFeatureAvailability;
    private readonly ITextExtractionFeatureAvailability _textExtractionFeatureAvailability;
    private readonly IImageDescriptionFeatureAvailability _imageDescriptionFeatureAvailability;
    private readonly IImageForegroundExtractionFeatureAvailability _imageForegroundExtractionFeatureAvailability;
    private readonly IImageObjectEraseFeatureAvailability _imageObjectEraseFeatureAvailability;
    private readonly IImageObjectExtractionFeatureAvailability _imageObjectExtractionFeatureAvailability;
    private readonly IVideoSuperResolutionFeatureAvailability _videoSuperResolutionFeatureAvailability;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IAppMetricsService _appMetricsService;
    private readonly IStoreService _storeService;
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;
    private readonly ITelemetryConsentService? _telemetryConsentService;
    private readonly IAiModelStorageService? _aiModelStorageService;
    private readonly ICaptureAnalysisSettingsConfirmationDialogService?
        _captureAnalysisConfirmationService;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public IAsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    public IAsyncRelayCommand OpenScreenshotsFolderCommand { get; }
    public IAsyncRelayCommand ChangeAudioFolderCommand { get; }
    public IAsyncRelayCommand OpenAudioFolderCommand { get; }
    public IAsyncRelayCommand ChangeVideosFolderCommand { get; }
    public IAsyncRelayCommand OpenVideosFolderCommand { get; }
    public IAsyncRelayCommand RestartAppCommand { get; }
    public IAsyncRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateAudioCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateAudioCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateAudioCaptureDefaultLocalAudioCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureDefaultLocalAudioCommand { get; }
    public IAsyncRelayCommand<bool> UpdateCaptureWarnBeforeDiscardCommand { get; }
    public IAsyncRelayCommand<bool> UpdateEditWarnBeforeDiscardCommand { get; }
    public IAsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    public IAsyncRelayCommand<int> UpdateAppThemeCommand { get; }
    public IAsyncRelayCommand OpenTemporaryFilesFolderCommand { get; }
    public IAsyncRelayCommand ClearTemporaryFilesCommand { get; }
    public IAsyncRelayCommand RestoreDefaultSettingsCommand { get; }
    public IAsyncRelayCommand OpenStoreReviewCommand { get; }
    public IAsyncRelayCommand<bool> UpdateStoreReviewRemindersEnabledCommand { get; }
    public IAsyncRelayCommand<bool> UpdateOptionalUsageDataEnabledCommand { get; }
    public IAsyncRelayCommand RemoveDownloadedAiModelsCommand { get; }

    public ObservableCollection<AppLanguageViewModel> AppLanguages
    {
        get;
        private set => Set(ref field, value);
    }

    public int SelectedAppLanguageIndex
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ShowAppLanguageRestartMessage
    {
        get;
        private set => Set(ref field, value);
    }

    public ObservableCollection<AppThemeViewModel> AppThemes
    {
        get;
        private set => Set(ref field, value);
    }

    public ObservableCollection<AiFeatureConsentViewModel> AiFeatureConsents
    {
        get;
        private set => Set(ref field, value);
    }

    public bool IsAiConsentSettingsVisible
    {
        get;
        private set => Set(ref field, value);
    }

    public int SelectedAppThemeIndex
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ShowAppThemeRestartMessage
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ImageCaptureAutoCopy
    {
        get;
        private set => Set(ref field, value);
    }

    public bool ImageCaptureAutoSave
    {
        get;
        private set => Set(ref field, value);
    }

    public bool VideoCaptureAutoCopy
    {
        get;
        private set => Set(ref field, value);
    }

    public bool VideoCaptureAutoSave
    {
        get;
        private set => Set(ref field, value);
    }

    public bool AudioCaptureAutoCopy
    {
        get;
        private set => Set(ref field, value);
    }

    public bool AudioCaptureAutoSave
    {
        get;
        private set => Set(ref field, value);
    }

    public bool AudioCaptureDefaultLocalAudio
    {
        get;
        private set => Set(ref field, value);
    }

    public bool VideoCaptureDefaultLocalAudio
    {
        get;
        private set => Set(ref field, value);
    }

    public bool EditWarnBeforeDiscard
    {
        get;
        private set => Set(ref field, value);
    }

    public bool CaptureWarnBeforeDiscard
    {
        get;
        private set => Set(ref field, value);
    }

    public bool StoreReviewRemindersEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public bool OptionalUsageDataEnabled
    {
        get;
        private set => Set(ref field, value);
    }

    public string ScreenshotsFolderPath
    {
        get;
        private set => Set(ref field, value);
    }

    public string VideosFolderPath
    {
        get;
        private set => Set(ref field, value);
    }

    public string AudioFolderPath
    {
        get;
        private set => Set(ref field, value);
    }

    public string TemporaryFilesFolderPath
    {
        get;
        private set => Set(ref field, value);
    }

    public string TemporaryFilesStatusText
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(HasTemporaryFilesStatus));
            }
        }
    }

    public bool HasTemporaryFilesStatus =>
        !string.IsNullOrWhiteSpace(TemporaryFilesStatusText);

    public bool IsAiModelStorageVisible => _aiModelStorageService != null;

    public long DownloadedAiModelByteCount
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanRemoveDownloadedAiModels));
                RemoveDownloadedAiModelsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAiModelStorageMeasured
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanRemoveDownloadedAiModels));
                RemoveDownloadedAiModelsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsAiModelStorageBusy
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(CanRemoveDownloadedAiModels));
                RemoveDownloadedAiModelsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string AiModelStorageSummaryText
    {
        get;
        private set => Set(ref field, value);
    }

    public string AiModelStorageStatusText
    {
        get;
        private set
        {
            if (Set(ref field, value))
            {
                RaisePropertyChanged(nameof(HasAiModelStorageStatus));
            }
        }
    }

    public bool HasAiModelStorageStatus =>
        !string.IsNullOrWhiteSpace(AiModelStorageStatusText);

    public bool CanRemoveDownloadedAiModels =>
        _aiModelStorageService != null &&
        _captureAnalysisConfirmationService != null &&
        IsAiModelStorageMeasured &&
        DownloadedAiModelByteCount > 0 &&
        !IsAiModelStorageBusy &&
        (!CaptureMemory.IsAuthorized || !CaptureMemory.IsAnalyzingNewCaptures);

    public CaptureMemorySettingsViewModel CaptureMemory { get; }

    public SettingsPageViewModel(
        ILeaveSettingsPageUseCase goBackAction,
        IRestartSettingsApplicationUseCase restartAppAction,
        IUpdateImageAutoCopyUseCase updateImageAutoCopyAction,
        IUpdateImageAutoSaveUseCase updateImageAutoSaveAction,
        IUpdateAudioCaptureAutoCopyUseCase updateAudioCaptureAutoCopyAction,
        IUpdateAudioCaptureAutoSaveUseCase updateAudioCaptureAutoSaveAction,
        IUpdateAudioCaptureDefaultLocalAudioUseCase updateAudioCaptureDefaultLocalAudioAction,
        IUpdateVideoCaptureAutoCopyUseCase updateVideoCaptureAutoCopyAction,
        IUpdateVideoCaptureAutoSaveUseCase updateVideoCaptureAutoSaveAction,
        IUpdateVideoCaptureDefaultLocalAudioUseCase updateVideoCaptureDefaultLocalAudioAction,
        IUpdateCaptureWarnBeforeDiscardUseCase updateCaptureWarnBeforeDiscardAction,
        IUpdateEditWarnBeforeDiscardUseCase updateEditWarnBeforeDiscardAction,
        IUpdateAppLanguageUseCase updateAppLanguageAction,
        IUpdateAppThemeUseCase updateAppThemeAction,
        IChangeScreenshotsFolderUseCase changeScreenshotsFolderAction,
        IOpenScreenshotsFolderUseCase openScreenshotsFolderAction,
        IChangeAudioFolderUseCase changeAudioFolderAction,
        IOpenAudioFolderUseCase openAudioFolderAction,
        IChangeVideosFolderUseCase changeVideosFolderAction,
        IOpenVideosFolderUseCase openVideosFolderAction,
        IOpenTempFolderUseCase openTempFolderAction,
        IClearTempFilesUseCase clearTempFilesAction,
        IRestoreDefaultsUseCase restoreDefaultsAction,
        IAiFeatureConsentService aiFeatureConsentService,
        IAiConsentSettingsFeatureAvailability aiConsentSettingsFeatureAvailability,
        IImageSuperResolutionFeatureAvailability imageSuperResolutionFeatureAvailability,
        ITextExtractionFeatureAvailability textExtractionFeatureAvailability,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        IAppMetricsService appMetricsService,
        IStoreService storeService,
        IStorageService storageService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory,
        IImageDescriptionFeatureAvailability? imageDescriptionFeatureAvailability = null,
        IImageForegroundExtractionFeatureAvailability? imageForegroundExtractionFeatureAvailability = null,
        IImageObjectEraseFeatureAvailability? imageObjectEraseFeatureAvailability = null,
        IImageObjectExtractionFeatureAvailability? imageObjectExtractionFeatureAvailability = null,
        IVideoSuperResolutionFeatureAvailability? videoSuperResolutionFeatureAvailability = null,
        ITelemetryConsentService? telemetryConsentService = null,
        CaptureMemorySettingsViewModel? captureMemory = null,
        IAiModelStorageService? aiModelStorageService = null,
        ICaptureAnalysisSettingsConfirmationDialogService?
            captureAnalysisConfirmationService = null)
    {
        _goBackAction = goBackAction;
        _restartAppAction = restartAppAction;
        _updateImageAutoCopyAction = updateImageAutoCopyAction;
        _updateImageAutoSaveAction = updateImageAutoSaveAction;
        _updateAudioCaptureAutoCopyAction = updateAudioCaptureAutoCopyAction;
        _updateAudioCaptureAutoSaveAction = updateAudioCaptureAutoSaveAction;
        _updateAudioCaptureDefaultLocalAudioAction = updateAudioCaptureDefaultLocalAudioAction;
        _updateVideoCaptureAutoCopyAction = updateVideoCaptureAutoCopyAction;
        _updateVideoCaptureAutoSaveAction = updateVideoCaptureAutoSaveAction;
        _updateVideoCaptureDefaultLocalAudioAction = updateVideoCaptureDefaultLocalAudioAction;
        _updateCaptureWarnBeforeDiscardAction = updateCaptureWarnBeforeDiscardAction;
        _updateEditWarnBeforeDiscardAction = updateEditWarnBeforeDiscardAction;
        _updateAppLanguageAction = updateAppLanguageAction;
        _updateAppThemeAction = updateAppThemeAction;
        _changeScreenshotsFolderAction = changeScreenshotsFolderAction;
        _openScreenshotsFolderAction = openScreenshotsFolderAction;
        _changeAudioFolderAction = changeAudioFolderAction;
        _openAudioFolderAction = openAudioFolderAction;
        _changeVideosFolderAction = changeVideosFolderAction;
        _openVideosFolderAction = openVideosFolderAction;
        _openTempFolderAction = openTempFolderAction;
        _clearTempFilesAction = clearTempFilesAction;
        _restoreDefaultsAction = restoreDefaultsAction;
        _aiFeatureConsentService = aiFeatureConsentService;
        _aiConsentSettingsFeatureAvailability = aiConsentSettingsFeatureAvailability;
        _imageSuperResolutionFeatureAvailability = imageSuperResolutionFeatureAvailability;
        _textExtractionFeatureAvailability = textExtractionFeatureAvailability;
        _imageDescriptionFeatureAvailability = imageDescriptionFeatureAvailability ?? new DisabledImageDescriptionFeatureAvailability();
        _imageForegroundExtractionFeatureAvailability = imageForegroundExtractionFeatureAvailability ?? new DisabledImageForegroundExtractionFeatureAvailability();
        _imageObjectEraseFeatureAvailability = imageObjectEraseFeatureAvailability ?? new DisabledImageObjectEraseFeatureAvailability();
        _imageObjectExtractionFeatureAvailability = imageObjectExtractionFeatureAvailability ?? new DisabledImageObjectExtractionFeatureAvailability();
        _videoSuperResolutionFeatureAvailability =
            videoSuperResolutionFeatureAvailability ?? new DisabledVideoSuperResolutionFeatureAvailability();
        _telemetryConsentService = telemetryConsentService;
        _aiModelStorageService = aiModelStorageService;
        _captureAnalysisConfirmationService = captureAnalysisConfirmationService;
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;
        _appMetricsService = appMetricsService;
        _storeService = storeService;
        _storageService = storageService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;
        CaptureMemory = captureMemory ?? new CaptureMemorySettingsViewModel();
        CaptureMemory.PropertyChanged += CaptureMemory_PropertyChanged;

        AppThemes = [];
        AppLanguages = [];
        AiFeatureConsents = [];
        ScreenshotsFolderPath = string.Empty;
        AudioFolderPath = string.Empty;
        VideosFolderPath = string.Empty;
        TemporaryFilesFolderPath = string.Empty;
        TemporaryFilesStatusText = string.Empty;
        AiModelStorageSummaryText = string.Empty;
        AiModelStorageStatusText = string.Empty;

        ChangeScreenshotsFolderCommand = new AsyncRelayCommand(ChangeScreenshotsFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenScreenshotsFolderCommand = new AsyncRelayCommand(OpenScreenshotsFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ChangeAudioFolderCommand = new AsyncRelayCommand(ChangeAudioFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenAudioFolderCommand = new AsyncRelayCommand(OpenAudioFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ChangeVideosFolderCommand = new AsyncRelayCommand(ChangeVideosFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenVideosFolderCommand = new AsyncRelayCommand(OpenVideosFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RestartAppCommand = new AsyncRelayCommand(RestartAppAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateImageCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoCopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateImageCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoSaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateAudioCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateAudioCaptureAutoCopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateAudioCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateAudioCaptureAutoSaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateAudioCaptureDefaultLocalAudioCommand = new AsyncRelayCommand<bool>(UpdateAudioCaptureDefaultLocalAudioAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateVideoCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoCopyAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateVideoCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoSaveAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateVideoCaptureDefaultLocalAudioCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureDefaultLocalAudioAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateCaptureWarnBeforeDiscardCommand = new AsyncRelayCommand<bool>(UpdateCaptureWarnBeforeDiscardAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateEditWarnBeforeDiscardCommand = new AsyncRelayCommand<bool>(UpdateEditWarnBeforeDiscardAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateAppLanguageCommand = new AsyncRelayCommand<int>(UpdateAppLanguageAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateAppThemeCommand = new AsyncRelayCommand<int>(UpdateAppThemeAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenTemporaryFilesFolderCommand = new AsyncRelayCommand(OpenTemporaryFilesFolderAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        ClearTemporaryFilesCommand = new AsyncRelayCommand(ClearTemporaryFilesAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RestoreDefaultSettingsCommand = new AsyncRelayCommand(RestoreDefaultSettingsAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        OpenStoreReviewCommand = new AsyncRelayCommand(OpenStoreReviewAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateStoreReviewRemindersEnabledCommand = new AsyncRelayCommand<bool>(UpdateStoreReviewRemindersEnabledAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        UpdateOptionalUsageDataEnabledCommand = new AsyncRelayCommand<bool>(UpdateOptionalUsageDataEnabledAsync, AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
        RemoveDownloadedAiModelsCommand = new AsyncRelayCommand(
            RemoveDownloadedAiModelsAsync,
            () => CanRemoveDownloadedAiModels,
            AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler);
    }

    public override async Task LoadAsync(CancellationToken cancellationToken)
    {
        ThrowIfNotReadyToLoad();
        StartLoading();

        // Languages
        IAppLanguage[] languages = _localizationService.SupportedLanguages;
        int appLanguageIndex = -1;
        for (var i = 0; i < languages.Length; i++)
        {
            IAppLanguage language = languages[i];
            AppLanguageViewModel vm = _appLanguageViewModelFactory.Create(language);
            AppLanguages.Add(vm);

            if (language.Value == _localizationService.LanguageOverride?.Value)
            {
                appLanguageIndex = i;
            }
        }
        AppLanguages.Add(_appLanguageViewModelFactory.Create(null)); // Null for system default
        if (appLanguageIndex != -1)
        {
            SelectedAppLanguageIndex = appLanguageIndex;
        }
        else
        {
            SelectedAppLanguageIndex = AppLanguages.Count - 1;
        }
        UpdateShowAppLanguageRestartMessage();

        // Themes
        AppTheme currentTheme = _themeService.CurrentTheme;
        int appThemeIndex = -1;
        AppThemes.Clear();
        for (var i = 0; i < SupportedAppThemes.Length; i++)
        {
            AppTheme supportedTheme = SupportedAppThemes[i];
            AppThemeViewModel vm = _appThemeViewModelFactory.Create(supportedTheme);
            AppThemes.Add(vm);

            if (supportedTheme == currentTheme)
            {
                appThemeIndex = i;
            }
        }
        if (appThemeIndex != -1)
        {
            SelectedAppThemeIndex = appThemeIndex;
        }
        else
        {
            SelectedAppThemeIndex = SupportedAppThemes.IndexOf(AppTheme.SystemDefault);
        }
        UpdateShowAppThemeRestartMessage();

        ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
        ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

        VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
        VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
        VideoCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        AudioCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoCopy);
        AudioCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSave);
        AudioCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled);
        CaptureWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard);
        EditWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard);
        StoreReviewRemindersEnabled = _appMetricsService.StoreReviewRemindersEnabled;
        OptionalUsageDataEnabled =
            TelemetryConsentSettingValues.Parse(
                _settingsService.Get(CaptureToolSettings.Settings_TelemetryConsent)) ==
            TelemetryConsentState.Granted;
        RefreshAiFeatureConsents();

        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
        }

        ScreenshotsFolderPath = screenshotsFolder;

        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(videosFolder))
        {
            videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
        }

        VideosFolderPath = videosFolder;

        string audioFolder = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(audioFolder))
        {
            audioFolder = _storageService.GetSystemDefaultMusicFolderPath();
        }

        AudioFolderPath = audioFolder;
        TemporaryFilesFolderPath = _storageService.GetApplicationScratchFolderPath();

        await CaptureMemory.LoadAsync(cancellationToken);
        await RefreshAiModelStorageAsync(cancellationToken);

        await base.LoadAsync(cancellationToken);
    }

    public override void Dispose()
    {
        CaptureMemory.PropertyChanged -= CaptureMemory_PropertyChanged;
        CaptureMemory.Dispose();
        base.Dispose();
    }

    public async Task UpdateAiFeatureConsentAsync(AiFeatureId featureId, bool isConsented)
    {
        bool saved = await _aiFeatureConsentService.SetConsentAsync(
            featureId,
            isConsented,
            CancellationToken.None);
        if (!saved)
        {
            return;
        }

        AiFeatureConsentViewModel? featureConsent = AiFeatureConsents.FirstOrDefault(consent => consent.FeatureId == featureId);
        featureConsent?.ApplyConsent(isConsented);
    }

    private async Task UpdateAppLanguageAsync(int index)
    {
        SelectedAppLanguageIndex = index;
        if (SelectedAppLanguageIndex == -1)
        {
            return;
        }

        AppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
        if (vm.Language == _localizationService.LanguageOverride)
        {
            return;
        }

        var response = await _updateAppLanguageAction.ExecuteAsync(
            new UpdateAppLanguageRequest(index),
            CancellationToken.None);
        if (response.Value?.Succeeded != true)
        {
            SelectedAppLanguageIndex = GetCommittedAppLanguageIndex();
        }

        UpdateShowAppLanguageRestartMessage();
    }

    private int GetCommittedAppLanguageIndex()
    {
        string? languageOverride = _localizationService.LanguageOverride?.Value;
        for (var index = 0; index < AppLanguages.Count - 1; index++)
        {
            if (AppLanguages[index].Language?.Value == languageOverride)
            {
                return index;
            }
        }

        return AppLanguages.Count - 1;
    }

    private void UpdateShowAppLanguageRestartMessage()
    {
        ShowAppLanguageRestartMessage =
            _localizationService.RequestedLanguage != _localizationService.StartupLanguage ||
            (_localizationService.LanguageOverride == null && _localizationService.StartupLanguage != _localizationService.DefaultLanguage);
    }

    private async Task UpdateAppThemeAsync(int index)
    {
        SelectedAppThemeIndex = index;
        if (SelectedAppThemeIndex == -1)
        {
            return;
        }

        await _updateAppThemeAction.ExecuteAsync(new UpdateAppThemeRequest(index), CancellationToken.None);
        UpdateShowAppThemeRestartMessage();
    }

    private void UpdateShowAppThemeRestartMessage()
    {
        var defaultTheme = _themeService.DefaultTheme;
        var startupTheme = _themeService.StartupTheme;
        var currentTheme = _themeService.CurrentTheme;

        // Make sure currentTheme is light or dark.
        // defaultTheme is never "SystemDefault".
        if (currentTheme == AppTheme.SystemDefault)
        {
            currentTheme = defaultTheme;
        }

        if (startupTheme == AppTheme.SystemDefault)
        {
            startupTheme = defaultTheme;
        }

        ShowAppThemeRestartMessage = currentTheme != startupTheme;
    }

    private async Task UpdateImageCaptureAutoSaveAsync(bool value)
    {
        var response = await _updateImageAutoSaveAction.ExecuteAsync(
            new UpdateImageAutoSaveRequest(value),
            CancellationToken.None);
        ImageCaptureAutoSave = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);
    }

    private async Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        var response = await _updateImageAutoCopyAction.ExecuteAsync(
            new UpdateImageAutoCopyRequest(value),
            CancellationToken.None);
        ImageCaptureAutoCopy = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
    }

    private async Task UpdateVideoCaptureAutoSaveAsync(bool value)
    {
        var response = await _updateVideoCaptureAutoSaveAction.ExecuteAsync(
            new UpdateVideoCaptureAutoSaveRequest(value),
            CancellationToken.None);
        VideoCaptureAutoSave = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
    }

    private async Task UpdateVideoCaptureAutoCopyAsync(bool value)
    {
        var response = await _updateVideoCaptureAutoCopyAction.ExecuteAsync(
            new UpdateVideoCaptureAutoCopyRequest(value),
            CancellationToken.None);
        VideoCaptureAutoCopy = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
    }

    private async Task UpdateAudioCaptureAutoSaveAsync(bool value)
    {
        var response = await _updateAudioCaptureAutoSaveAction.ExecuteAsync(
            new UpdateAudioCaptureAutoSaveRequest(value),
            CancellationToken.None);
        AudioCaptureAutoSave = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSave);
    }

    private async Task UpdateAudioCaptureAutoCopyAsync(bool value)
    {
        var response = await _updateAudioCaptureAutoCopyAction.ExecuteAsync(
            new UpdateAudioCaptureAutoCopyRequest(value),
            CancellationToken.None);
        AudioCaptureAutoCopy = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoCopy);
    }

    private async Task UpdateVideoCaptureDefaultLocalAudioAsync(bool value)
    {
        var response = await _updateVideoCaptureDefaultLocalAudioAction.ExecuteAsync(
            new UpdateVideoCaptureDefaultLocalAudioRequest(value),
            CancellationToken.None);
        VideoCaptureDefaultLocalAudio = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
    }

    private async Task UpdateAudioCaptureDefaultLocalAudioAsync(bool value)
    {
        var response = await _updateAudioCaptureDefaultLocalAudioAction.ExecuteAsync(
            new UpdateAudioCaptureDefaultLocalAudioRequest(value),
            CancellationToken.None);
        AudioCaptureDefaultLocalAudio = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled);
    }

    private async Task UpdateEditWarnBeforeDiscardAsync(bool value)
    {
        var response = await _updateEditWarnBeforeDiscardAction.ExecuteAsync(
            new UpdateEditWarnBeforeDiscardRequest(value),
            CancellationToken.None);
        EditWarnBeforeDiscard = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard);
    }

    private async Task UpdateCaptureWarnBeforeDiscardAsync(bool value)
    {
        var response = await _updateCaptureWarnBeforeDiscardAction.ExecuteAsync(
            new UpdateCaptureWarnBeforeDiscardRequest(value),
            CancellationToken.None);
        CaptureWarnBeforeDiscard = response.Value?.Succeeded == true
            ? value
            : _settingsService.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard);
    }

    private async Task UpdateStoreReviewRemindersEnabledAsync(bool value)
    {
        StoreReviewRemindersEnabled = value;
        await _appMetricsService.SetStoreReviewRemindersEnabledAsync(value, CancellationToken.None);
    }

    private async Task UpdateOptionalUsageDataEnabledAsync(bool value)
    {
        TelemetryConsentState state = value
            ? TelemetryConsentState.Granted
            : TelemetryConsentState.Denied;
        SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
            CaptureToolSettings.Settings_TelemetryConsent,
            TelemetryConsentSettingValues.Serialize(state),
            CancellationToken.None);
        if (!result.Succeeded)
        {
            OptionalUsageDataEnabled =
                TelemetryConsentSettingValues.Parse(
                    _settingsService.Get(CaptureToolSettings.Settings_TelemetryConsent)) ==
                TelemetryConsentState.Granted;
            return;
        }

        OptionalUsageDataEnabled = value;
        _telemetryConsentService?.SetState(state);
    }

    private async Task OpenStoreReviewAsync()
    {
        await _storeService.LaunchAppReviewAsync(CancellationToken.None);
    }

    private async Task ChangeScreenshotsFolderAsync()
    {
        var response = await _changeScreenshotsFolderAction.ExecuteAsync(new ChangeScreenshotsFolderRequest(), CancellationToken.None);
        if (response.Value?.Changed != true)
        {
            return;
        }

        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
        }
        ScreenshotsFolderPath = screenshotsFolder;
    }

    private async Task ChangeVideosFolderAsync()
    {
        var response = await _changeVideosFolderAction.ExecuteAsync(new ChangeVideosFolderRequest(), CancellationToken.None);
        if (response.Value?.Changed != true)
        {
            return;
        }

        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(videosFolder))
        {
            videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
        }
        VideosFolderPath = videosFolder;
    }

    private async Task ChangeAudioFolderAsync()
    {
        var response = await _changeAudioFolderAction.ExecuteAsync(new ChangeAudioFolderRequest(), CancellationToken.None);
        if (response.Value?.Changed != true)
        {
            return;
        }

        string audioFolder = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(audioFolder))
        {
            audioFolder = _storageService.GetSystemDefaultMusicFolderPath();
        }
        AudioFolderPath = audioFolder;
    }

    private async Task OpenScreenshotsFolderAsync()
    {
        await _openScreenshotsFolderAction.ExecuteAsync(new OpenScreenshotsFolderRequest(), CancellationToken.None);
    }

    private async Task OpenVideosFolderAsync()
    {
        await _openVideosFolderAction.ExecuteAsync(new OpenVideosFolderRequest(), CancellationToken.None);
    }

    private async Task OpenAudioFolderAsync()
    {
        await _openAudioFolderAction.ExecuteAsync(new OpenAudioFolderRequest(), CancellationToken.None);
    }

    private async Task RestartAppAsync()
    {
        await _restartAppAction.ExecuteAsync(new RestartSettingsApplicationRequest(), CancellationToken.None);
    }

    private async Task GoBackAsync()
    {
        await _goBackAction.ExecuteAsync(new LeaveSettingsPageRequest(), CancellationToken.None);
    }

    private async Task ClearTemporaryFilesAsync()
    {
        TemporaryFilesStatusText = GetString(
            "Settings_TemporaryFolder_Clearing",
            "Clearing temporary working files…");
        UseCaseResponse<ClearTempFilesResponse> response =
            await _clearTempFilesAction.ExecuteAsync(
                new ClearTempFilesRequest(),
                CancellationToken.None);
        if (response.Result != UseCaseResult.Succeeded || response.Value == null)
        {
            TemporaryFilesStatusText = GetString(
                "Settings_TemporaryFolder_ClearFailed",
                "Temporary working files could not be cleared.");
            return;
        }

        ClearTempFilesResponse result = response.Value;
        string deletedBytes = FormatByteCount(result.DeletedByteCount);
        if (result.FailedItemCount > 0)
        {
            TemporaryFilesStatusText = string.Format(
                CultureInfo.CurrentCulture,
                GetString(
                    "Settings_TemporaryFolder_ClearIncomplete",
                    "Cleared {0} from {1} temporary item(s), but {2} item(s) could not be removed. {3} active item(s) were kept."),
                deletedBytes,
                result.DeletedItemCount,
                result.FailedItemCount,
                result.ActiveItemCount);
            return;
        }

        if (result.DeletedItemCount == 0)
        {
            TemporaryFilesStatusText = string.Format(
                CultureInfo.CurrentCulture,
                GetString(
                    "Settings_TemporaryFolder_NothingToClear",
                    "No unused temporary working files were found. {0} active item(s) were kept."),
                result.ActiveItemCount);
            return;
        }

        TemporaryFilesStatusText = string.Format(
            CultureInfo.CurrentCulture,
            GetString(
                "Settings_TemporaryFolder_ClearSucceeded",
                "Cleared {0} from {1} temporary item(s). {2} active item(s) were kept."),
            deletedBytes,
            result.DeletedItemCount,
            result.ActiveItemCount);
    }

    private async Task OpenTemporaryFilesFolderAsync()
    {
        await _openTempFolderAction.ExecuteAsync(new OpenTempFolderRequest(), CancellationToken.None);
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        var response = await _restoreDefaultsAction.ExecuteAsync(
            new RestoreDefaultsRequest(),
            CancellationToken.None);
        if (response.Value?.Succeeded != true)
        {
            return;
        }

        ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
        ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

        VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
        VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
        VideoCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        AudioCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoCopy);
        AudioCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSave);
        AudioCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled);
        CaptureWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard);
        EditWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard);
        RefreshAiFeatureConsents();
        OptionalUsageDataEnabled = false;
        _telemetryConsentService?.SetState(TelemetryConsentState.Unknown);

        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        ScreenshotsFolderPath = !string.IsNullOrEmpty(screenshotsFolder) ? screenshotsFolder : _storageService.GetSystemDefaultScreenshotsFolderPath();

        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        VideosFolderPath = !string.IsNullOrEmpty(videosFolder) ? videosFolder : _storageService.GetSystemDefaultVideosFolderPath();

        string audioFolder = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder);
        AudioFolderPath = !string.IsNullOrEmpty(audioFolder) ? audioFolder : _storageService.GetSystemDefaultMusicFolderPath();

        SelectedAppLanguageIndex = AppLanguages.Count - 1;
        SelectedAppThemeIndex = AppThemes.Count - 1;

        UpdateShowAppLanguageRestartMessage();
        UpdateShowAppThemeRestartMessage();
    }

    private void RefreshAiFeatureConsents()
    {
        AiFeatureConsents.Clear();

        if (!_aiConsentSettingsFeatureAvailability.IsAiConsentSettingsEnabled)
        {
            IsAiConsentSettingsVisible = false;
            return;
        }

        foreach (AiFeatureConsent consent in _aiFeatureConsentService.GetFeatureConsents())
        {
            if (!IsAiFeatureConsentVisible(consent.FeatureId))
            {
                continue;
            }

            AiFeatureConsents.Add(new(
                consent.FeatureId,
                GetAiFeatureDisplayName(consent),
                GetString(
                    "Settings_AiConsentDescription",
                    "Allow this on-device AI editing tool when you choose to use it."),
                consent.State == AiFeatureConsentState.Granted));
        }

        IsAiConsentSettingsVisible = AiFeatureConsents.Count > 0;
    }

    private string GetAiFeatureDisplayName(AiFeatureConsent consent)
    {
        string? resourceKey = consent.FeatureId switch
        {
            AiFeatureId.TextExtraction => "Settings_AiConsent_TextExtractionDisplayName",
            AiFeatureId.ImageSuperResolution => "Settings_AiConsent_ImageSuperResolutionDisplayName",
            AiFeatureId.ImageDescription => "Settings_AiConsent_ImageDescriptionDisplayName",
            AiFeatureId.ImageForegroundExtraction => "Settings_AiConsent_ImageForegroundExtractionDisplayName",
            AiFeatureId.ImageObjectErase => "Settings_AiConsent_ImageObjectEraseDisplayName",
            AiFeatureId.ImageObjectExtraction => "Settings_AiConsent_ImageObjectExtractionDisplayName",
            AiFeatureId.VideoSuperResolution => "Settings_AiConsent_VideoSuperResolutionDisplayName",
            _ => null
        };

        if (resourceKey is null)
        {
            return consent.DisplayName;
        }

        string localizedDisplayName = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(localizedDisplayName)
            ? consent.DisplayName
            : localizedDisplayName;
    }

    private async Task RefreshAiModelStorageAsync(CancellationToken cancellationToken)
    {
        if (_aiModelStorageService == null)
        {
            return;
        }

        AiModelStorageSnapshot snapshot = await _aiModelStorageService
            .GetSnapshotAsync(cancellationToken)
            .ConfigureAwait(true);
        DownloadedAiModelByteCount = snapshot.DownloadedByteCount;
        IsAiModelStorageMeasured = snapshot.MeasurementSucceeded;
        AiModelStorageSummaryText = snapshot.MeasurementSucceeded
            ? string.Format(
                CultureInfo.CurrentCulture,
                GetString(
                    "Settings_AiModelStorage_Size",
                    "Downloaded on-device models use {0}."),
                FormatByteCount(snapshot.DownloadedByteCount))
            : GetString(
                "Settings_AiModelStorage_SizeUnavailable",
                "Downloaded model storage could not be measured.");
    }

    private async Task RemoveDownloadedAiModelsAsync()
    {
        if (_aiModelStorageService == null ||
            _captureAnalysisConfirmationService == null ||
            !CanRemoveDownloadedAiModels)
        {
            AiModelStorageStatusText = GetString(
                "Settings_AiModelStorage_PauseRequired",
                "Pause analysis of new captures before removing downloaded models.");
            return;
        }

        CaptureAnalysisConfirmationDecision decision =
            await _captureAnalysisConfirmationService.ConfirmAsync(
                new CaptureAnalysisSettingsConfirmationRequest(
                    CaptureAnalysisSettingsAction.RemoveDownloadedModels),
                CancellationToken.None);
        if (decision != CaptureAnalysisConfirmationDecision.Confirmed)
        {
            return;
        }

        IsAiModelStorageBusy = true;
        AiModelStorageStatusText = GetString(
            "Settings_AiModelStorage_Removing",
            "Removing downloaded AI models…");
        try
        {
            AiModelStorageRemovalResult result = await _aiModelStorageService
                .RemoveDownloadedModelsAsync(CancellationToken.None);
            DownloadedAiModelByteCount = result.RemainingByteCount;
            IsAiModelStorageMeasured = true;
            AiModelStorageSummaryText = string.Format(
                CultureInfo.CurrentCulture,
                GetString(
                    "Settings_AiModelStorage_Size",
                    "Downloaded on-device models use {0}."),
                FormatByteCount(result.RemainingByteCount));

            if (!result.Succeeded)
            {
                AiModelStorageStatusText = string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "Settings_AiModelStorage_RemoveIncomplete",
                        "Removed {0} model(s), but {1} model(s) could not be removed. Try again after restarting the app."),
                    result.RemovedModelCount,
                    result.FailedModelCount);
                return;
            }

            AiModelStorageStatusText = result.RemovedModelCount == 0
                ? GetString(
                    "Settings_AiModelStorage_NothingToRemove",
                    "No downloaded AI models were found.")
                : string.Format(
                    CultureInfo.CurrentCulture,
                    GetString(
                        "Settings_AiModelStorage_RemoveSucceeded",
                        "Removed {0} downloaded model(s) and freed {1}. Capture files and analyzed metadata were kept."),
                    result.RemovedModelCount,
                    FormatByteCount(result.ReclaimedByteCount));
        }
        finally
        {
            IsAiModelStorageBusy = false;
        }
    }

    private void CaptureMemory_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CaptureMemorySettingsViewModel.IsAuthorized) or
            nameof(CaptureMemorySettingsViewModel.IsAnalyzingNewCaptures))
        {
            RaisePropertyChanged(nameof(CanRemoveDownloadedAiModels));
            RemoveDownloadedAiModelsCommand.NotifyCanExecuteChanged();
        }
    }

    private static string FormatByteCount(long byteCount)
    {
        const double OneKilobyte = 1024;
        const double OneMegabyte = OneKilobyte * 1024;
        const double OneGigabyte = OneMegabyte * 1024;
        return byteCount switch
        {
            < 1024 => string.Format(CultureInfo.CurrentCulture, "{0:N0} B", byteCount),
            < (long)OneMegabyte => string.Format(
                CultureInfo.CurrentCulture,
                "{0:N1} KB",
                byteCount / OneKilobyte),
            < (long)OneGigabyte => string.Format(
                CultureInfo.CurrentCulture,
                "{0:N1} MB",
                byteCount / OneMegabyte),
            _ => string.Format(
                CultureInfo.CurrentCulture,
                "{0:N1} GB",
                byteCount / OneGigabyte),
        };
    }

    private string GetString(string resourceKey, string fallback)
    {
        string value = _localizationService.GetString(resourceKey);
        return string.IsNullOrWhiteSpace(value) || value == resourceKey ? fallback : value;
    }

    private bool IsAiFeatureConsentVisible(AiFeatureId featureId)
    {
        return featureId switch
        {
            AiFeatureId.TextExtraction => _textExtractionFeatureAvailability.IsTextExtractionEnabled,
            AiFeatureId.ImageSuperResolution => _imageSuperResolutionFeatureAvailability.IsImageSuperResolutionEnabled,
            AiFeatureId.ImageDescription => _imageDescriptionFeatureAvailability.IsImageDescriptionEnabled,
            AiFeatureId.ImageForegroundExtraction => _imageForegroundExtractionFeatureAvailability.IsImageForegroundExtractionEnabled,
            AiFeatureId.ImageObjectErase => _imageObjectEraseFeatureAvailability.IsImageObjectEraseEnabled,
            AiFeatureId.ImageObjectExtraction => _imageObjectExtractionFeatureAvailability.IsImageObjectExtractionEnabled,
            AiFeatureId.VideoSuperResolution => _videoSuperResolutionFeatureAvailability.IsVideoSuperResolutionEnabled,
            _ => false
        };
    }

    private sealed class DisabledImageDescriptionFeatureAvailability : IImageDescriptionFeatureAvailability
    {
        public bool IsImageDescriptionEnabled => false;
    }

    private sealed class DisabledImageForegroundExtractionFeatureAvailability : IImageForegroundExtractionFeatureAvailability
    {
        public bool IsImageForegroundExtractionEnabled => false;
    }

    private sealed class DisabledImageObjectEraseFeatureAvailability : IImageObjectEraseFeatureAvailability
    {
        public bool IsImageObjectEraseEnabled => false;
    }

    private sealed class DisabledImageObjectExtractionFeatureAvailability : IImageObjectExtractionFeatureAvailability
    {
        public bool IsImageObjectExtractionEnabled => false;
    }

    private sealed class DisabledVideoSuperResolutionFeatureAvailability :
        IVideoSuperResolutionFeatureAvailability
    {
        public bool IsVideoSuperResolutionEnabled => false;
    }
}
