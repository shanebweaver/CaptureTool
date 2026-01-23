using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Infrastructure.Implementations.UseCases.Extensions;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.FeatureManagement;
using CaptureTool.Application.Interfaces.Settings;
using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.FeatureManagement;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Settings;
using CaptureTool.Infrastructure.Interfaces.Storage;
using CaptureTool.Infrastructure.Interfaces.Telemetry;
using CaptureTool.Infrastructure.Interfaces.Themes;
using CaptureTool.Application.Implementations.ViewModels.Helpers;
using System.Collections.ObjectModel;

namespace CaptureTool.Application.Implementations.ViewModels;

public sealed partial class SettingsPageViewModel : AsyncLoadableViewModelBase, ISettingsPageViewModel
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadSettingsPage";
        public static readonly string RestartApp = "RestartApp";
        public static readonly string GoBack = "GoBack";
        public static readonly string UpdateImageCaptureAutoCopy = "UpdateImageCaptureAutoCopy";
        public static readonly string UpdateImageCaptureAutoSave = "UpdateImageCaptureAutoSave";
        public static readonly string UpdateVideoCaptureAutoCopy = "UpdateVideoCaptureAutoCopy";
        public static readonly string UpdateVideoCaptureAutoSave = "UpdateVideoCaptureAutoSave";
        public static readonly string ChangeScreenshotsFolder = "ChangeScreenshotsFolder";
        public static readonly string ChangeVideosFolder = "ChangeVideosFolder";
        public static readonly string OpenScreenshotsFolder = "OpenScreenshotsFolder";
        public static readonly string OpenVideosFolder = "OpenVideosFolder";
        public static readonly string UpdateAppLanguage = "UpdateAppLanguage";
        public static readonly string UpdateAppTheme = "UpdateAppTheme";
        public static readonly string UpdateShowAppThemeRestartMessage = "UpdateShowAppThemeRestartMessage";
        public static readonly string UpdateShowAppLanguageRestartMessage = "UpdateShowAppLanguageRestartMessage";
        public static readonly string ClearTemporaryFiles = "ClearTemporaryFiles";
        public static readonly string OpenTemporaryFilesFolder = "OpenTemporaryFilesFolder";
        public static readonly string RestoreDefaultSettings = "RestoreDefaultSettings";
    }

    private const string TelemetryContext = "SettingsPage";

    private readonly ISettingsGoBackUseCase _goBackAction;
    private readonly ISettingsRestartAppUseCase _restartAppAction;
    private readonly ISettingsUpdateImageAutoCopyUseCase _updateImageAutoCopyAction;
    private readonly ISettingsUpdateImageAutoSaveUseCase _updateImageAutoSaveAction;
    private readonly ISettingsUpdateVideoCaptureAutoCopyUseCase _updateVideoCaptureAutoCopyAction;
    private readonly ISettingsUpdateVideoCaptureAutoSaveUseCase _updateVideoCaptureAutoSaveAction;
    private readonly ISettingsUpdateAppLanguageUseCase _updateAppLanguageAction;
    private readonly ISettingsUpdateAppThemeUseCase _updateAppThemeAction;
    private readonly ISettingsChangeScreenshotsFolderUseCase _changeScreenshotsFolderAction;
    private readonly ISettingsOpenScreenshotsFolderUseCase _openScreenshotsFolderAction;
    private readonly ISettingsChangeVideosFolderUseCase _changeVideosFolderAction;
    private readonly ISettingsOpenVideosFolderUseCase _openVideosFolderAction;
    private readonly ISettingsOpenTempFolderUseCase _openTempFolderAction;
    private readonly ISettingsClearTempFilesUseCase _clearTempFilesAction;
    private readonly ISettingsRestoreDefaultsUseCase _restoreDefaultsAction;
    private readonly ITelemetryService _telemetryService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IStorageService _storageService;
    private readonly IFeatureManager _featureManager;
    private readonly IFactoryServiceWithArgs<IAppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<IAppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public AsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    public RelayCommand OpenScreenshotsFolderCommand { get; }
    public AsyncRelayCommand ChangeVideosFolderCommand { get; }
    public RelayCommand OpenVideosFolderCommand { get; }
    public RelayCommand RestartAppCommand { get; }
    public RelayCommand GoBackCommand { get; }
    public AsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    public AsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    public AsyncRelayCommand<bool> UpdateVideoCaptureAutoCopyCommand { get; }
    public AsyncRelayCommand<bool> UpdateVideoCaptureAutoSaveCommand { get; }
    public AsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    public RelayCommand<int> UpdateAppThemeCommand { get; }
    public RelayCommand OpenTemporaryFilesFolderCommand { get; }
    public RelayCommand ClearTemporaryFilesCommand { get; }
    public AsyncRelayCommand RestoreDefaultSettingsCommand { get; }

    public ObservableCollection<IAppLanguageViewModel> AppLanguages
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int SelectedAppLanguageIndex
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool ShowAppLanguageRestartMessage
    {
        get => field;
        private set => Set(ref field, value);
    }

    public ObservableCollection<IAppThemeViewModel> AppThemes
    {
        get => field;
        private set => Set(ref field, value);
    }

    public int SelectedAppThemeIndex
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool ShowAppThemeRestartMessage
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool IsVideoCaptureFeatureEnabled
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool ImageCaptureAutoCopy
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool ImageCaptureAutoSave
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool VideoCaptureAutoCopy
    {
        get => field;
        private set => Set(ref field, value);
    }

    public bool VideoCaptureAutoSave
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string ScreenshotsFolderPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string VideosFolderPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public string TemporaryFilesFolderPath
    {
        get => field;
        private set => Set(ref field, value);
    }

    public SettingsPageViewModel(
        ISettingsGoBackUseCase goBackAction,
        ISettingsRestartAppUseCase restartAppAction,
        ISettingsUpdateImageAutoCopyUseCase updateImageAutoCopyAction,
        ISettingsUpdateImageAutoSaveUseCase updateImageAutoSaveAction,
        ISettingsUpdateVideoCaptureAutoCopyUseCase updateVideoCaptureAutoCopyAction,
        ISettingsUpdateVideoCaptureAutoSaveUseCase updateVideoCaptureAutoSaveAction,
        ISettingsUpdateAppLanguageUseCase updateAppLanguageAction,
        ISettingsUpdateAppThemeUseCase updateAppThemeAction,
        ISettingsChangeScreenshotsFolderUseCase changeScreenshotsFolderAction,
        ISettingsOpenScreenshotsFolderUseCase openScreenshotsFolderAction,
        ISettingsChangeVideosFolderUseCase changeVideosFolderAction,
        ISettingsOpenVideosFolderUseCase openVideosFolderAction,
        ISettingsOpenTempFolderUseCase openTempFolderAction,
        ISettingsClearTempFilesUseCase clearTempFilesAction,
        ISettingsRestoreDefaultsUseCase restoreDefaultsAction,
        ITelemetryService telemetryService,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        IStorageService storageService,
        IFeatureManager featureManager,
        IFactoryServiceWithArgs<IAppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<IAppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _goBackAction = goBackAction;
        _restartAppAction = restartAppAction;
        _updateImageAutoCopyAction = updateImageAutoCopyAction;
        _updateImageAutoSaveAction = updateImageAutoSaveAction;
        _updateVideoCaptureAutoCopyAction = updateVideoCaptureAutoCopyAction;
        _updateVideoCaptureAutoSaveAction = updateVideoCaptureAutoSaveAction;
        _updateAppLanguageAction = updateAppLanguageAction;
        _updateAppThemeAction = updateAppThemeAction;
        _changeScreenshotsFolderAction = changeScreenshotsFolderAction;
        _openScreenshotsFolderAction = openScreenshotsFolderAction;
        _changeVideosFolderAction = changeVideosFolderAction;
        _openVideosFolderAction = openVideosFolderAction;
        _openTempFolderAction = openTempFolderAction;
        _clearTempFilesAction = clearTempFilesAction;
        _restoreDefaultsAction = restoreDefaultsAction;
        _telemetryService = telemetryService;
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

        TelemetryCommandFactory commandFactory = new(telemetryService, TelemetryContext);
        ChangeScreenshotsFolderCommand = commandFactory.CreateAsync(ActivityIds.ChangeScreenshotsFolder, ChangeScreenshotsFolderAsync);
        OpenScreenshotsFolderCommand = commandFactory.Create(ActivityIds.OpenScreenshotsFolder, OpenScreenshotsFolder);
        ChangeVideosFolderCommand = commandFactory.CreateAsync(ActivityIds.ChangeVideosFolder, ChangeVideosFolderAsync);
        OpenVideosFolderCommand = commandFactory.Create(ActivityIds.OpenVideosFolder, OpenVideosFolder);
        RestartAppCommand = commandFactory.Create(ActivityIds.RestartApp, RestartApp);
        GoBackCommand = commandFactory.Create(ActivityIds.GoBack, GoBack);
        UpdateImageCaptureAutoCopyCommand = commandFactory.CreateAsync<bool>(ActivityIds.UpdateImageCaptureAutoCopy, UpdateImageCaptureAutoCopyAsync);
        UpdateImageCaptureAutoSaveCommand = commandFactory.CreateAsync<bool>(ActivityIds.UpdateImageCaptureAutoSave, UpdateImageCaptureAutoSaveAsync);
        UpdateVideoCaptureAutoCopyCommand = commandFactory.CreateAsync<bool>(ActivityIds.UpdateVideoCaptureAutoCopy, UpdateVideoCaptureAutoCopyAsync);
        UpdateVideoCaptureAutoSaveCommand = commandFactory.CreateAsync<bool>(ActivityIds.UpdateVideoCaptureAutoSave, UpdateVideoCaptureAutoSaveAsync);
        UpdateAppLanguageCommand = commandFactory.CreateAsync<int>(ActivityIds.UpdateAppLanguage, UpdateAppLanguageAsync);
        UpdateAppThemeCommand = commandFactory.Create<int>(ActivityIds.UpdateAppTheme, UpdateAppTheme);
        OpenTemporaryFilesFolderCommand = commandFactory.Create(ActivityIds.OpenTemporaryFilesFolder, OpenTemporaryFilesFolder);
        ClearTemporaryFilesCommand = commandFactory.Create(ActivityIds.ClearTemporaryFiles, ClearTemporaryFiles);
        RestoreDefaultSettingsCommand = commandFactory.CreateAsync(ActivityIds.RestoreDefaultSettings, RestoreDefaultSettingsAsync);
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, TelemetryContext, ActivityIds.Load, async () =>
        {
            ThrowIfNotReadyToLoad();
            StartLoading();

            // Languages
            IAppLanguage[] languages = _localizationService.SupportedLanguages;
            int appLanguageIndex = -1;
            for (var i = 0; i < languages.Length; i++)
            {
                IAppLanguage language = languages[i];
                IAppLanguageViewModel vm = _appLanguageViewModelFactory.Create(language);
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
                IAppThemeViewModel vm = _appThemeViewModelFactory.Create(supportedTheme);
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

            IsVideoCaptureFeatureEnabled = _featureManager.IsEnabled(CaptureToolFeatures.Feature_VideoCapture);

            ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

            VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
            VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);

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
        });
    }

    private async Task UpdateAppLanguageAsync(int index)
    {
        SelectedAppLanguageIndex = index;
        if (SelectedAppLanguageIndex == -1)
        {
            return;
        }

        IAppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
        if (vm.Language == _localizationService.LanguageOverride)
        {
            return;
        }

        await _updateAppLanguageAction.ExecuteCommandAsync(index, CancellationToken.None);
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

        _updateAppThemeAction.ExecuteCommand(index);
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
        await _updateImageAutoSaveAction.ExecuteCommandAsync(value, CancellationToken.None);
    }

    private async Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        ImageCaptureAutoCopy = value;
        await _updateImageAutoCopyAction.ExecuteCommandAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoCaptureAutoSaveAsync(bool value)
    {
        VideoCaptureAutoSave = value;
        await _updateVideoCaptureAutoSaveAction.ExecuteCommandAsync(value, CancellationToken.None);
    }

    private async Task UpdateVideoCaptureAutoCopyAsync(bool value)
    {
        VideoCaptureAutoCopy = value;
        await _updateVideoCaptureAutoCopyAction.ExecuteCommandAsync(value, CancellationToken.None);
    }

    private async Task ChangeScreenshotsFolderAsync()
    {
        await _changeScreenshotsFolderAction.ExecuteCommandAsync(CancellationToken.None);
        
        var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(screenshotsFolder))
        {
            screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
        }
        ScreenshotsFolderPath = screenshotsFolder;
    }

    private async Task ChangeVideosFolderAsync()
    {
        await _changeVideosFolderAction.ExecuteCommandAsync(CancellationToken.None);
        
        var videosFolder = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
        if (string.IsNullOrWhiteSpace(videosFolder))
        {
            videosFolder = _storageService.GetSystemDefaultVideosFolderPath();
        }
        VideosFolderPath = videosFolder;
    }

    private void OpenScreenshotsFolder()
    {
        _openScreenshotsFolderAction.ExecuteCommand();
    }

    private void OpenVideosFolder()
    {
        _openVideosFolderAction.ExecuteCommand();
    }

    private void RestartApp()
    {
        _restartAppAction.ExecuteCommand();
    }

    private void GoBack()
    {
        _goBackAction.ExecuteCommand();
    }

    private void ClearTemporaryFiles()
    {
        _clearTempFilesAction.ExecuteCommand(TemporaryFilesFolderPath);
    }

    private void OpenTemporaryFilesFolder()
    {
        _openTempFolderAction.ExecuteCommand();
    }

    private async Task RestoreDefaultSettingsAsync()
    {
        await _restoreDefaultsAction.ExecuteCommandAsync(CancellationToken.None);

        ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
        ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

        VideoCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoCopy);
        VideoCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSave);

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
