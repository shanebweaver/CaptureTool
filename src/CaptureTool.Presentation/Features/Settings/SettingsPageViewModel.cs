using CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;
using CaptureTool.Application.Abstractions.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Settings.UpdateCaptureWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateEditWarnBeforeDiscard;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Factories;
using CaptureTool.Presentation.Shared.Commands;
using CaptureTool.Presentation.ViewModels;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

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
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;
    private readonly ITelemetryService? _telemetryService;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

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
    public IAsyncRelayCommand<bool> UpdateTelemetryEnabledCommand { get; }
    public IAsyncRelayCommand ResetTelemetryInstallIdCommand { get; }

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

    public bool TelemetryEnabled
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
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        IStorageService storageService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory,
        ITelemetryService? telemetryService = null)
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
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;
        _storageService = storageService;
        _telemetryService = telemetryService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        AppThemes = [];
        AppLanguages = [];
        ScreenshotsFolderPath = string.Empty;
        AudioFolderPath = string.Empty;
        VideosFolderPath = string.Empty;
        TemporaryFilesFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = TelemetryCommandFactory.Async("settings.change_screenshots_folder", ChangeScreenshotsFolderAsync, telemetryService, "settings");
        OpenScreenshotsFolderCommand = TelemetryCommandFactory.Async("settings.open_screenshots_folder", OpenScreenshotsFolderAsync, telemetryService, "settings");
        ChangeAudioFolderCommand = TelemetryCommandFactory.Async("settings.change_audio_folder", ChangeAudioFolderAsync, telemetryService, "settings");
        OpenAudioFolderCommand = TelemetryCommandFactory.Async("settings.open_audio_folder", OpenAudioFolderAsync, telemetryService, "settings");
        ChangeVideosFolderCommand = TelemetryCommandFactory.Async("settings.change_videos_folder", ChangeVideosFolderAsync, telemetryService, "settings");
        OpenVideosFolderCommand = TelemetryCommandFactory.Async("settings.open_videos_folder", OpenVideosFolderAsync, telemetryService, "settings");
        RestartAppCommand = TelemetryCommandFactory.Async("settings.restart_app", RestartAppAsync, telemetryService, "settings");
        GoBackCommand = TelemetryCommandFactory.Async("settings.go_back", GoBackAsync, telemetryService, "settings");
        UpdateImageCaptureAutoCopyCommand = TelemetryCommandFactory.Async<bool>("settings.update_image_auto_copy", UpdateImageCaptureAutoCopyAsync, telemetryService, "settings");
        UpdateImageCaptureAutoSaveCommand = TelemetryCommandFactory.Async<bool>("settings.update_image_auto_save", UpdateImageCaptureAutoSaveAsync, telemetryService, "settings");
        UpdateAudioCaptureAutoCopyCommand = TelemetryCommandFactory.Async<bool>("settings.update_audio_auto_copy", UpdateAudioCaptureAutoCopyAsync, telemetryService, "settings");
        UpdateAudioCaptureAutoSaveCommand = TelemetryCommandFactory.Async<bool>("settings.update_audio_auto_save", UpdateAudioCaptureAutoSaveAsync, telemetryService, "settings");
        UpdateAudioCaptureDefaultLocalAudioCommand = TelemetryCommandFactory.Async<bool>("settings.update_audio_default_local_audio", UpdateAudioCaptureDefaultLocalAudioAsync, telemetryService, "settings");
        UpdateVideoCaptureAutoCopyCommand = TelemetryCommandFactory.Async<bool>("settings.update_video_auto_copy", UpdateVideoCaptureAutoCopyAsync, telemetryService, "settings");
        UpdateVideoCaptureAutoSaveCommand = TelemetryCommandFactory.Async<bool>("settings.update_video_auto_save", UpdateVideoCaptureAutoSaveAsync, telemetryService, "settings");
        UpdateVideoCaptureDefaultLocalAudioCommand = TelemetryCommandFactory.Async<bool>("settings.update_video_default_local_audio", UpdateVideoCaptureDefaultLocalAudioAsync, telemetryService, "settings");
        UpdateCaptureWarnBeforeDiscardCommand = TelemetryCommandFactory.Async<bool>("settings.update_capture_warn_before_discard", UpdateCaptureWarnBeforeDiscardAsync, telemetryService, "settings");
        UpdateEditWarnBeforeDiscardCommand = TelemetryCommandFactory.Async<bool>("settings.update_edit_warn_before_discard", UpdateEditWarnBeforeDiscardAsync, telemetryService, "settings");
        UpdateAppLanguageCommand = TelemetryCommandFactory.Async<int>("settings.update_app_language", UpdateAppLanguageAsync, telemetryService, "settings");
        UpdateAppThemeCommand = TelemetryCommandFactory.Async<int>("settings.update_app_theme", UpdateAppThemeAsync, telemetryService, "settings");
        OpenTemporaryFilesFolderCommand = TelemetryCommandFactory.Async("settings.open_temporary_files_folder", OpenTemporaryFilesFolderAsync, telemetryService, "settings");
        ClearTemporaryFilesCommand = TelemetryCommandFactory.Async("settings.clear_temporary_files", ClearTemporaryFilesAsync, telemetryService, "settings");
        RestoreDefaultSettingsCommand = TelemetryCommandFactory.Async("settings.restore_default_settings", RestoreDefaultSettingsAsync, telemetryService, "settings");
        UpdateTelemetryEnabledCommand = TelemetryCommandFactory.Async<bool>("settings.update_telemetry_enabled", UpdateTelemetryEnabledAsync, telemetryService, "settings");
        ResetTelemetryInstallIdCommand = TelemetryCommandFactory.Async("settings.reset_telemetry_install_id", ResetTelemetryInstallIdAsync, telemetryService, "settings");
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
        TelemetryEnabled = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_IsEnabled);

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
        TemporaryFilesFolderPath = _storageService.GetApplicationTemporaryFolderPath();

        await base.LoadAsync(cancellationToken);
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

        await _updateAppLanguageAction.ExecuteAsync(new UpdateAppLanguageRequest(index), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_LanguageOverride.Key);
        UpdateShowAppLanguageRestartMessage();
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
        TrackSettingChanged("AppTheme");
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
        ImageCaptureAutoSave = value;
        await _updateImageAutoSaveAction.ExecuteAsync(new UpdateImageAutoSaveRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_ImageCapture_AutoSave.Key);
    }

    private async Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        ImageCaptureAutoCopy = value;
        await _updateImageAutoCopyAction.ExecuteAsync(new UpdateImageAutoCopyRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_ImageCapture_AutoCopy.Key);
    }

    private async Task UpdateVideoCaptureAutoSaveAsync(bool value)
    {
        VideoCaptureAutoSave = value;
        await _updateVideoCaptureAutoSaveAction.ExecuteAsync(new UpdateVideoCaptureAutoSaveRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_VideoCapture_AutoSave.Key);
    }

    private async Task UpdateVideoCaptureAutoCopyAsync(bool value)
    {
        VideoCaptureAutoCopy = value;
        await _updateVideoCaptureAutoCopyAction.ExecuteAsync(new UpdateVideoCaptureAutoCopyRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_VideoCapture_AutoCopy.Key);
    }

    private async Task UpdateAudioCaptureAutoSaveAsync(bool value)
    {
        AudioCaptureAutoSave = value;
        await _updateAudioCaptureAutoSaveAction.ExecuteAsync(new UpdateAudioCaptureAutoSaveRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_AudioCapture_AutoSave.Key);
    }

    private async Task UpdateAudioCaptureAutoCopyAsync(bool value)
    {
        AudioCaptureAutoCopy = value;
        await _updateAudioCaptureAutoCopyAction.ExecuteAsync(new UpdateAudioCaptureAutoCopyRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_AudioCapture_AutoCopy.Key);
    }

    private async Task UpdateVideoCaptureDefaultLocalAudioAsync(bool value)
    {
        VideoCaptureDefaultLocalAudio = value;
        await _updateVideoCaptureDefaultLocalAudioAction.ExecuteAsync(new UpdateVideoCaptureDefaultLocalAudioRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled.Key);
    }

    private async Task UpdateAudioCaptureDefaultLocalAudioAsync(bool value)
    {
        AudioCaptureDefaultLocalAudio = value;
        await _updateAudioCaptureDefaultLocalAudioAction.ExecuteAsync(new UpdateAudioCaptureDefaultLocalAudioRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled.Key);
    }

    private async Task UpdateEditWarnBeforeDiscardAsync(bool value)
    {
        EditWarnBeforeDiscard = value;
        await _updateEditWarnBeforeDiscardAction.ExecuteAsync(new UpdateEditWarnBeforeDiscardRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_Edit_WarnBeforeDiscard.Key);
    }

    private async Task UpdateCaptureWarnBeforeDiscardAsync(bool value)
    {
        CaptureWarnBeforeDiscard = value;
        await _updateCaptureWarnBeforeDiscardAction.ExecuteAsync(new UpdateCaptureWarnBeforeDiscardRequest(value), CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard.Key);
    }

    private async Task UpdateTelemetryEnabledAsync(bool value)
    {
        TelemetryEnabled = value;
        _settingsService.Set(CaptureToolSettings.Settings_Telemetry_IsEnabled, value);
        await _settingsService.TrySaveAsync(CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_Telemetry_IsEnabled.Key);
    }

    private async Task ResetTelemetryInstallIdAsync()
    {
        _settingsService.Set(CaptureToolSettings.Settings_Telemetry_InstallId, Guid.NewGuid().ToString("N"));
        await _settingsService.TrySaveAsync(CancellationToken.None);
        TrackSettingChanged(CaptureToolSettings.Settings_Telemetry_InstallId.Key);
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
        TrackSettingChanged(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder.Key);
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
        TrackSettingChanged(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder.Key);
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
        TrackSettingChanged(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder.Key);
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
        await _clearTempFilesAction.ExecuteAsync(new ClearTempFilesRequest(), CancellationToken.None);
    }

    private async Task OpenTemporaryFilesFolderAsync()
    {
        await _openTempFolderAction.ExecuteAsync(new OpenTempFolderRequest(), CancellationToken.None);
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        await _restoreDefaultsAction.ExecuteAsync(new RestoreDefaultsRequest(), CancellationToken.None);

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
        TelemetryEnabled = _settingsService.Get(CaptureToolSettings.Settings_Telemetry_IsEnabled);

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
        TrackSettingChanged("settings.restore_defaults");
    }

    private void TrackSettingChanged(string settingKey)
    {
        _telemetryService?.TrackEvent(
            TelemetryEvents.SettingsChanged,
            new Dictionary<string, object?>
            {
                [TelemetryAttributes.SettingKey] = settingKey,
                [TelemetryAttributes.Surface] = "settings"
            });
    }
}
