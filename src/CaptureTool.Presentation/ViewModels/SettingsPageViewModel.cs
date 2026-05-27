using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Settings;
using CaptureTool.FeatureManagement;
using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Factories;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Storage;
using CaptureTool.Infrastructure.Abstractions.Telemetry;
using CaptureTool.Infrastructure.Abstractions.Themes;
using CaptureTool.Infrastructure.Commands;
using CaptureTool.Infrastructure.ViewModels;
using CaptureTool.Presentation.ViewModels.Helpers;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace CaptureTool.Presentation.ViewModels;

public sealed partial class SettingsPageViewModel : AsyncLoadableViewModelBase
{
    private readonly ILeaveSettingsPageAppCommand _goBackAction;
    private readonly ISettingsRestartApplicationAppCommand _restartAppAction;
    private readonly ISettingsUpdateImageAutoCopyAppCommand _updateImageAutoCopyAction;
    private readonly ISettingsUpdateImageAutoSaveAppCommand _updateImageAutoSaveAction;
    private readonly ISettingsUpdateVideoCaptureAutoCopyAppCommand _updateVideoCaptureAutoCopyAction;
    private readonly ISettingsUpdateVideoCaptureAutoSaveAppCommand _updateVideoCaptureAutoSaveAction;
    private readonly ISettingsUpdateVideoCaptureDefaultLocalAudioAppCommand _updateVideoCaptureDefaultLocalAudioAction;
    private readonly ISettingsUpdateVideoMetadataAutoSaveAppCommand _updateVideoMetadataAutoSaveAction;
    private readonly ISettingsUpdateAppLanguageAppCommand _updateAppLanguageAction;
    private readonly ISettingsUpdateAppThemeAppCommand _updateAppThemeAction;
    private readonly IChangeScreenshotsFolderAppCommand _changeScreenshotsFolderAction;
    private readonly ISettingsOpenScreenshotsFolderAppCommand _openScreenshotsFolderAction;
    private readonly ISettingsChangeVideosFolderAppCommand _changeVideosFolderAction;
    private readonly ISettingsOpenVideosFolderAppCommand _openVideosFolderAction;
    private readonly ISettingsOpenTempFolderAppCommand _openTempFolderAction;
    private readonly ISettingsClearTempFilesAppCommand _clearTempFilesAction;
    private readonly ISettingsRestoreDefaultsAppCommand _restoreDefaultsAction;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;
    private readonly IFeatureManager _featureManager;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];


    public IAsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    public IRelayCommand OpenScreenshotsFolderCommand { get; }
    public IAsyncRelayCommand ChangeVideosFolderCommand { get; }
    public IRelayCommand OpenVideosFolderCommand { get; }
    public IRelayCommand RestartAppCommand { get; }
    public IRelayCommand GoBackCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoCaptureDefaultLocalAudioCommand { get; }
    public IAsyncRelayCommand<bool> UpdateVideoMetadataAutoSaveCommand { get; }
    public IAsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    public IRelayCommand<int> UpdateAppThemeCommand { get; }
    public IRelayCommand OpenTemporaryFilesFolderCommand { get; }
    public IRelayCommand ClearTemporaryFilesCommand { get; }
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

    public bool IsVideoMetadataFeatureEnabled
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

    public bool VideoMetadataAutoSave
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
        ILeaveSettingsPageAppCommand goBackAction,
        ISettingsRestartApplicationAppCommand restartAppAction,
        ISettingsUpdateImageAutoCopyAppCommand updateImageAutoCopyAction,
        ISettingsUpdateImageAutoSaveAppCommand updateImageAutoSaveAction,
        ISettingsUpdateVideoCaptureAutoCopyAppCommand updateVideoCaptureAutoCopyAction,
        ISettingsUpdateVideoCaptureAutoSaveAppCommand updateVideoCaptureAutoSaveAction,
        ISettingsUpdateVideoCaptureDefaultLocalAudioAppCommand updateVideoCaptureDefaultLocalAudioAction,
        ISettingsUpdateVideoMetadataAutoSaveAppCommand updateVideoMetadataAutoSaveAction,
        ISettingsUpdateAppLanguageAppCommand updateAppLanguageAction,
        ISettingsUpdateAppThemeAppCommand updateAppThemeAction,
        IChangeScreenshotsFolderAppCommand changeScreenshotsFolderAction,
        ISettingsOpenScreenshotsFolderAppCommand openScreenshotsFolderAction,
        ISettingsChangeVideosFolderAppCommand changeVideosFolderAction,
        ISettingsOpenVideosFolderAppCommand openVideosFolderAction,
        ISettingsOpenTempFolderAppCommand openTempFolderAction,
        ISettingsClearTempFilesAppCommand clearTempFilesAction,
        ISettingsRestoreDefaultsAppCommand restoreDefaultsAction,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        IStorageService storageService,
        IFeatureManager featureManager,
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
        _updateVideoMetadataAutoSaveAction = updateVideoMetadataAutoSaveAction;
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
        _featureManager = featureManager;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        AppThemes = [];
        AppLanguages = [];
        ScreenshotsFolderPath = string.Empty;
        VideosFolderPath = string.Empty;
        TemporaryFilesFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = new AsyncRelayCommand(ChangeScreenshotsFolderAsync);
        OpenScreenshotsFolderCommand = new RelayCommand(OpenScreenshotsFolder);
        ChangeVideosFolderCommand = new AsyncRelayCommand(ChangeVideosFolderAsync);
        OpenVideosFolderCommand = new RelayCommand(OpenVideosFolder);
        RestartAppCommand = new RelayCommand(RestartApp);
        GoBackCommand = new RelayCommand(GoBack);
        UpdateImageCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoCopyAsync);
        UpdateImageCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateImageCaptureAutoSaveAsync);
        UpdateVideoCaptureAutoCopyCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoCopyAsync);
        UpdateVideoCaptureAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureAutoSaveAsync);
        UpdateVideoCaptureDefaultLocalAudioCommand = new AsyncRelayCommand<bool>(UpdateVideoCaptureDefaultLocalAudioAsync);
        UpdateVideoMetadataAutoSaveCommand = new AsyncRelayCommand<bool>(UpdateVideoMetadataAutoSaveAsync);
        UpdateAppLanguageCommand = new AsyncRelayCommand<int>(UpdateAppLanguageAsync);
        UpdateAppThemeCommand = new RelayCommand<int>(UpdateAppTheme);
        OpenTemporaryFilesFolderCommand = new RelayCommand(OpenTemporaryFilesFolder);
        ClearTemporaryFilesCommand = new RelayCommand(ClearTemporaryFiles);
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

        IsVideoMetadataFeatureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture_MetadataCollection);

        ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
        ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

        VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
        VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
        VideoCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        VideoMetadataAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave);

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

        await _updateAppLanguageAction.ExecuteAsync(index, CancellationToken.None);
        UpdateShowAppLanguageRestartMessage();
    }

    private void UpdateShowAppLanguageRestartMessage()
    {
        ShowAppLanguageRestartMessage =
            _localizationService.RequestedLanguage != _localizationService.StartupLanguage ||
            (_localizationService.LanguageOverride == null && _localizationService.StartupLanguage != _localizationService.DefaultLanguage);
    }

    private void UpdateAppTheme(int index)
    {
        SelectedAppThemeIndex = index;
        if (SelectedAppThemeIndex == -1)
        {
            return;
        }

        _updateAppThemeAction.Execute(index);
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
        await _updateImageAutoSaveAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        ImageCaptureAutoCopy = value;
        await _updateImageAutoCopyAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoCaptureAutoSaveAsync(bool value)
    {
        VideoCaptureAutoSave = value;
        await _updateVideoCaptureAutoSaveAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoCaptureAutoCopyAsync(bool value)
    {
        VideoCaptureAutoCopy = value;
        await _updateVideoCaptureAutoCopyAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoMetadataAutoSaveAsync(bool value)
    {
        VideoMetadataAutoSave = value;
        await _updateVideoMetadataAutoSaveAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoCaptureDefaultLocalAudioAsync(bool value)
    {
        VideoCaptureDefaultLocalAudio = value;
        await _updateVideoCaptureDefaultLocalAudioAction.ExecuteAsync(value, CancellationToken.None);
    }

    private async Task ChangeScreenshotsFolderAsync()
    {
        await _changeScreenshotsFolderAction.ExecuteAsync(CancellationToken.None);

        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
        }
        ScreenshotsFolderPath = screenshotsFolder;
    }

    private async Task ChangeVideosFolderAsync()
    {
        await _changeVideosFolderAction.ExecuteAsync(CancellationToken.None);

        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(videosFolder))
        {
            videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
        }
        VideosFolderPath = videosFolder;
    }

    private void OpenScreenshotsFolder()
    {
        _openScreenshotsFolderAction.Execute();
    }

    private void OpenVideosFolder()
    {
        _openVideosFolderAction.Execute();
    }

    private void RestartApp()
    {
        _restartAppAction.Execute();
    }

    private void GoBack()
    {
        _goBackAction.Execute();
    }

    private void ClearTemporaryFiles()
    {
        _clearTempFilesAction.Execute();
    }

    private void OpenTemporaryFilesFolder()
    {
        _openTempFolderAction.Execute();
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        await _restoreDefaultsAction.ExecuteAsync(CancellationToken.None);

        ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
        ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

        VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
        VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);
        VideoCaptureDefaultLocalAudio = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled);
        VideoMetadataAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_MetadataAutoSave);

        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        ScreenshotsFolderPath = !string.IsNullOrEmpty(screenshotsFolder) ? screenshotsFolder : _storageService.GetSystemDefaultScreenshotsFolderPath();

        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        VideosFolderPath = !string.IsNullOrEmpty(videosFolder) ? videosFolder : _storageService.GetSystemDefaultVideosFolderPath();

        SelectedAppLanguageIndex = AppLanguages.Count - 1;
        SelectedAppThemeIndex = AppThemes.Count - 1;

        UpdateShowAppLanguageRestartMessage();
        UpdateShowAppThemeRestartMessage();
    }
}
