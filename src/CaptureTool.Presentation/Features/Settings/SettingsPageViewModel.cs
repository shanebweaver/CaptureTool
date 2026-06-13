using CaptureTool.Application.Abstractions.Features.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;
using CaptureTool.Application.Abstractions.Features.Settings.LeaveSettingsPage;
using CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Features.Settings.RestoreDefaults;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppLanguage;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateAppTheme;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateImageAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Presentation.Factories;
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
    private readonly IUpdateVideoCaptureAutoCopyUseCase _updateVideoCaptureAutoCopyAction;
    private readonly IUpdateVideoCaptureAutoSaveUseCase _updateVideoCaptureAutoSaveAction;
    private readonly IUpdateVideoCaptureDefaultLocalAudioUseCase _updateVideoCaptureDefaultLocalAudioAction;
    private readonly IUpdateAppLanguageUseCase _updateAppLanguageAction;
    private readonly IUpdateAppThemeUseCase _updateAppThemeAction;
    private readonly IChangeScreenshotsFolderUseCase _changeScreenshotsFolderAction;
    private readonly IOpenScreenshotsFolderUseCase _openScreenshotsFolderAction;
    private readonly IChangeVideosFolderUseCase _changeVideosFolderAction;
    private readonly IOpenVideosFolderUseCase _openVideosFolderAction;
    private readonly IOpenTempFolderUseCase _openTempFolderAction;
    private readonly IClearTempFilesUseCase _clearTempFilesAction;
    private readonly IRestoreDefaultsUseCase _restoreDefaultsAction;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;
    private readonly ITelemetryService _telemetryService;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];


    public IAsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    public IAsyncRelayCommand OpenScreenshotsFolderCommand { get; }
    public IAsyncRelayCommand ChangeVideosFolderCommand { get; }
    public IAsyncRelayCommand OpenVideosFolderCommand { get; }
    public IAsyncRelayCommand RestartAppCommand { get; }
    public IAsyncRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureDefaultLocalAudioCommand { get; }
    public IAsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    public IAsyncRelayCommand<int> UpdateAppThemeCommand { get; }
    public IAsyncRelayCommand OpenTemporaryFilesFolderCommand { get; }
    public IAsyncRelayCommand ClearTemporaryFilesCommand { get; }
    public IAsyncRelayCommand RestoreDefaultSettingsCommand { get; }

    private ObservableCollection<AppLanguageViewModel> _appLanguages = [];

    public ObservableCollection<AppLanguageViewModel> AppLanguages
    {
        get => _appLanguages;
        private set
        {
            _appLanguages = value;
            RaisePropertyChanged(nameof(AppLanguages));
        }
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

    private ObservableCollection<AppThemeViewModel> _appThemes = [];

    public ObservableCollection<AppThemeViewModel> AppThemes
    {
        get => _appThemes;
        private set
        {
            _appThemes = value;
            RaisePropertyChanged(nameof(AppThemes));
        }
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

    public bool VideoCaptureDefaultLocalAudio
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
        IUpdateVideoCaptureAutoCopyUseCase updateVideoCaptureAutoCopyAction,
        IUpdateVideoCaptureAutoSaveUseCase updateVideoCaptureAutoSaveAction,
        IUpdateVideoCaptureDefaultLocalAudioUseCase updateVideoCaptureDefaultLocalAudioAction,
        IUpdateAppLanguageUseCase updateAppLanguageAction,
        IUpdateAppThemeUseCase updateAppThemeAction,
        IChangeScreenshotsFolderUseCase changeScreenshotsFolderAction,
        IOpenScreenshotsFolderUseCase openScreenshotsFolderAction,
        IChangeVideosFolderUseCase changeVideosFolderAction,
        IOpenVideosFolderUseCase openVideosFolderAction,
        IOpenTempFolderUseCase openTempFolderAction,
        IClearTempFilesUseCase clearTempFilesAction,
        IRestoreDefaultsUseCase restoreDefaultsAction,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        IStorageService storageService,
        ITelemetryService telemetryService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _goBackAction = goBackAction;
        _restartAppAction = restartAppAction;
        _updateImageAutoCopyAction = updateImageAutoCopyAction;
        _updateImageAutoSaveAction = updateImageAutoSaveAction;
        _updateVideoCaptureAutoCopyAction = updateVideoCaptureAutoCopyAction;
        _updateVideoCaptureAutoSaveAction = updateVideoCaptureAutoSaveAction;
        _updateVideoCaptureDefaultLocalAudioAction = updateVideoCaptureDefaultLocalAudioAction;
        _updateAppLanguageAction = updateAppLanguageAction;
        _updateAppThemeAction = updateAppThemeAction;
        _changeScreenshotsFolderAction = changeScreenshotsFolderAction;
        _openScreenshotsFolderAction = openScreenshotsFolderAction;
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
        VideosFolderPath = string.Empty;
        TemporaryFilesFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = new AsyncRelayCommand(ChangeScreenshotsFolderAsync);
        OpenScreenshotsFolderCommand = new AsyncRelayCommand(OpenScreenshotsFolderAsync);
        ChangeVideosFolderCommand = new AsyncRelayCommand(ChangeVideosFolderAsync);
        OpenVideosFolderCommand = new AsyncRelayCommand(OpenVideosFolderAsync);
        RestartAppCommand = new AsyncRelayCommand(RestartAppAsync);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
        UpdateImageCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoCopyAsync);
        UpdateImageCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoSaveAsync);
        UpdateVideoCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoCopyAsync);
        UpdateVideoCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoSaveAsync);
        UpdateVideoCaptureDefaultLocalAudioCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureDefaultLocalAudioAsync);
        UpdateAppLanguageCommand = new AsyncRelayCommand<int>(UpdateAppLanguageAsync);
        UpdateAppThemeCommand = new AsyncRelayCommand<int>(UpdateAppThemeAsync);
        OpenTemporaryFilesFolderCommand = new AsyncRelayCommand(OpenTemporaryFilesFolderAsync);
        ClearTemporaryFilesCommand = new AsyncRelayCommand(ClearTemporaryFilesAsync);
        RestoreDefaultSettingsCommand = new AsyncRelayCommand(RestoreDefaultSettingsAsync);
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
            _appLanguages.Add(vm);

            if (language.Value == _localizationService.LanguageOverride?.Value)
            {
                appLanguageIndex = i;
            }
        }
        _appLanguages.Add(_appLanguageViewModelFactory.Create(null)); // Null for system default
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
        _appThemes.Clear();
        for (var i = 0; i < SupportedAppThemes.Length; i++)
        {
            AppTheme supportedTheme = SupportedAppThemes[i];
            AppThemeViewModel vm = _appThemeViewModelFactory.Create(supportedTheme);
            _appThemes.Add(vm);

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
        TemporaryFilesFolderPath = _storageService.GetApplicationTemporaryFolderPath();

        await base.LoadAsync(cancellationToken);
    }

    private async Task UpdateAppLanguageAsync(int index)
    {
        try
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
            UpdateShowAppLanguageRestartMessage();
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateAppLanguageAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateAppLanguageAsync), exception);
        }
    }

    private void UpdateShowAppLanguageRestartMessage()
    {
        ShowAppLanguageRestartMessage =
            _localizationService.RequestedLanguage != _localizationService.StartupLanguage ||
            (_localizationService.LanguageOverride == null && _localizationService.StartupLanguage != _localizationService.DefaultLanguage);
    }

    private async Task UpdateAppThemeAsync(int index)
    {
        try
        {
            SelectedAppThemeIndex = index;
            if (SelectedAppThemeIndex == -1)
            {
                return;
            }

            await _updateAppThemeAction.ExecuteAsync(new UpdateAppThemeRequest(index), CancellationToken.None);
            UpdateShowAppThemeRestartMessage();
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateAppThemeAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateAppThemeAsync), exception);
        }
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
        try
        {
            ImageCaptureAutoSave = value;
            await _updateImageAutoSaveAction.ExecuteAsync(new UpdateImageAutoSaveRequest(value), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateImageCaptureAutoSaveAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateImageCaptureAutoSaveAsync), exception);
        }
    }

    private async Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        try
        {
            ImageCaptureAutoCopy = value;
            await _updateImageAutoCopyAction.ExecuteAsync(new UpdateImageAutoCopyRequest(value), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateImageCaptureAutoCopyAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateImageCaptureAutoCopyAsync), exception);
        }
    }

    private async Task UpdateVideoCaptureAutoSaveAsync(bool value)
    {
        try
        {
            VideoCaptureAutoSave = value;
            await _updateVideoCaptureAutoSaveAction.ExecuteAsync(new UpdateVideoCaptureAutoSaveRequest(value), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateVideoCaptureAutoSaveAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateVideoCaptureAutoSaveAsync), exception);
        }
    }

    private async Task UpdateVideoCaptureAutoCopyAsync(bool value)
    {
        try
        {
            VideoCaptureAutoCopy = value;
            await _updateVideoCaptureAutoCopyAction.ExecuteAsync(new UpdateVideoCaptureAutoCopyRequest(value), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateVideoCaptureAutoCopyAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateVideoCaptureAutoCopyAsync), exception);
        }
    }

    private async Task UpdateVideoCaptureDefaultLocalAudioAsync(bool value)
    {
        try
        {
            VideoCaptureDefaultLocalAudio = value;
            await _updateVideoCaptureDefaultLocalAudioAction.ExecuteAsync(new UpdateVideoCaptureDefaultLocalAudioRequest(value), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(UpdateVideoCaptureDefaultLocalAudioAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(UpdateVideoCaptureDefaultLocalAudioAsync), exception);
        }
    }

    private async Task ChangeScreenshotsFolderAsync()
    {
        try
        {
            await _changeScreenshotsFolderAction.ExecuteAsync(new ChangeScreenshotsFolderRequest(), CancellationToken.None);

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
            }
            ScreenshotsFolderPath = screenshotsFolder;
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(ChangeScreenshotsFolderAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(ChangeScreenshotsFolderAsync), exception);
        }
    }

    private async Task ChangeVideosFolderAsync()
    {
        try
        {
            await _changeVideosFolderAction.ExecuteAsync(new ChangeVideosFolderRequest(), CancellationToken.None);

            var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
            if (string.IsNullOrWhiteSpace(videosFolder))
            {
                videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
            }
            VideosFolderPath = videosFolder;
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(ChangeVideosFolderAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(ChangeVideosFolderAsync), exception);
        }
    }

    private async Task OpenScreenshotsFolderAsync()
    {
        try
        {
            await _openScreenshotsFolderAction.ExecuteAsync(new OpenScreenshotsFolderRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(OpenScreenshotsFolderAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(OpenScreenshotsFolderAsync), exception);
        }
    }

    private async Task OpenVideosFolderAsync()
    {
        try
        {
            await _openVideosFolderAction.ExecuteAsync(new OpenVideosFolderRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(OpenVideosFolderAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(OpenVideosFolderAsync), exception);
        }
    }

    private async Task RestartAppAsync()
    {
        try
        {
            await _restartAppAction.ExecuteAsync(new RestartSettingsApplicationRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(RestartAppAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(RestartAppAsync), exception);
        }
    }

    private async Task GoBackAsync()
    {
        try
        {
            await _goBackAction.ExecuteAsync(new LeaveSettingsPageRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(GoBackAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(GoBackAsync), exception);
        }
    }

    private async Task ClearTemporaryFilesAsync()
    {
        try
        {
            await _clearTempFilesAction.ExecuteAsync(new ClearTempFilesRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(ClearTemporaryFilesAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(ClearTemporaryFilesAsync), exception);
        }
    }

    private async Task OpenTemporaryFilesFolderAsync()
    {
        try
        {
            await _openTempFolderAction.ExecuteAsync(new OpenTempFolderRequest(), CancellationToken.None);
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(OpenTemporaryFilesFolderAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(OpenTemporaryFilesFolderAsync), exception);
        }
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        try
        {
            await _restoreDefaultsAction.ExecuteAsync(new RestoreDefaultsRequest(), CancellationToken.None);

            ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

            VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
            VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
            VideoCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
            ScreenshotsFolderPath = !string.IsNullOrEmpty(screenshotsFolder) ? screenshotsFolder : _storageService.GetSystemDefaultScreenshotsFolderPath();

            var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
            VideosFolderPath = !string.IsNullOrEmpty(videosFolder) ? videosFolder : _storageService.GetSystemDefaultVideosFolderPath();

            SelectedAppLanguageIndex = AppLanguages.Count - 1;
            SelectedAppThemeIndex = AppThemes.Count - 1;

            UpdateShowAppLanguageRestartMessage();
            UpdateShowAppThemeRestartMessage();
        }
        catch (OperationCanceledException exception)
        {
            TrackCommandCancellation(nameof(RestoreDefaultSettingsAsync), exception);
        }
        catch (Exception exception)
        {
            TrackCommandError(nameof(RestoreDefaultSettingsAsync), exception);
        }
    }

    private void TrackCommandCancellation(string activityId, OperationCanceledException exception)
    {
        _telemetryService.ActivityCanceled(activityId, exception.Message);
    }

    private void TrackCommandError(string activityId, Exception exception)
    {
        _telemetryService.ActivityError(activityId, exception);
    }
}
