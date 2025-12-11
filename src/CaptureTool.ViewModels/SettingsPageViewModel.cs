using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Settings;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Services.Interfaces.Windowing;
using CaptureTool.ViewModels.Helpers;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : AsyncLoadableViewModelBase
{
    public readonly struct ActivityIds
    {
        public static readonly string Load = "LoadSettingsPage";
        public static readonly string RestartApp = "RestartApp";
        public static readonly string GoBack = "GoBack";
        public static readonly string UpdateImageCaptureAutoCopy = "UpdateImageCaptureAutoCopy";
        public static readonly string UpdateImageCaptureAutoSave = "UpdateImageCaptureAutoSave";
        public static readonly string ChangeScreenshotsFolder = "ChangeScreenshotsFolder";
        public static readonly string OpenScreenshotsFolder = "OpenScreenshotsFolder";
        public static readonly string UpdateAppLanguage = "UpdateAppLanguage";
        public static readonly string UpdateAppTheme = "UpdateAppTheme";
        public static readonly string UpdateShowAppThemeRestartMessage = "UpdateShowAppThemeRestartMessage";
        public static readonly string UpdateShowAppLanguageRestartMessage = "UpdateShowAppLanguageRestartMessage";
        public static readonly string ClearTemporaryFiles = "ClearTemporaryFiles";
        public static readonly string OpenTemporaryFilesFolder = "OpenTemporaryFilesFolder";
        public static readonly string RestoreDefaultSettings = "RestoreDefaultSettings";
    }

    private readonly ISettingsActions _settingsActions;
    private readonly ITelemetryService _telemetryService;
    private readonly IWindowHandleProvider _windowingService;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IFilePickerService _filePickerService;
    private readonly IStorageService _storageService;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public AsyncRelayCommand ChangeScreenshotsFolderCommand { get; }
    public RelayCommand OpenScreenshotsFolderCommand { get; }
    public RelayCommand RestartAppCommand { get; }
    public RelayCommand GoBackCommand { get; }
    public AsyncRelayCommand<bool> UpdateImageCaptureAutoCopyCommand { get; }
    public AsyncRelayCommand<bool> UpdateImageCaptureAutoSaveCommand { get; }
    public AsyncRelayCommand<int> UpdateAppLanguageCommand { get; }
    public RelayCommand<int> UpdateAppThemeCommand { get; }
    public RelayCommand OpenTemporaryFilesFolderCommand { get; }
    public RelayCommand ClearTemporaryFilesCommand { get; }
    public AsyncRelayCommand RestoreDefaultSettingsCommand { get; }

    public ObservableCollection<AppLanguageViewModel> AppLanguages
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

    public ObservableCollection<AppThemeViewModel> AppThemes
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

    public string ScreenshotsFolderPath
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
        ISettingsActions settingsActions,
        ITelemetryService telemetryService,
        IWindowHandleProvider windowingService,
        ILocalizationService localizationService,
        IThemeService themeService,
        IFilePickerService filePickerService,
        ISettingsService settingsService,
        IStorageService storageService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _settingsActions = settingsActions;
        _telemetryService = telemetryService;
        _windowingService = windowingService;
        _localizationService = localizationService;
        _themeService = themeService;
        _filePickerService = filePickerService;
        _settingsService = settingsService;
        _storageService = storageService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        AppThemes = [];
        AppLanguages = [];
        ScreenshotsFolderPath = string.Empty;
        TemporaryFilesFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = new(ChangeScreenshotsFolderAsync);
        OpenScreenshotsFolderCommand = new(OpenScreenshotsFolder);
        RestartAppCommand = new(RestartApp);
        GoBackCommand = new(GoBack);
        UpdateImageCaptureAutoCopyCommand = new(UpdateImageCaptureAutoCopyAsync);
        UpdateImageCaptureAutoSaveCommand = new(UpdateImageCaptureAutoSaveAsync);
        UpdateAppLanguageCommand = new(UpdateAppLanguageAsync);
        UpdateAppThemeCommand = new(UpdateAppTheme);
        OpenTemporaryFilesFolderCommand = new(OpenTemporaryFilesFolder);
        ClearTemporaryFilesCommand = new(ClearTemporaryFiles);
        RestoreDefaultSettingsCommand = new(RestoreDefaultSettingsAsync);
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
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

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
            }

            ScreenshotsFolderPath = screenshotsFolder;
            TemporaryFilesFolderPath = _storageService.GetApplicationTemporaryFolderPath();

            await base.LoadAsync(cancellationToken);
        });
    }

    private Task UpdateAppLanguageAsync(int index)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateAppLanguage, async () =>
        {
            SelectedAppLanguageIndex = index;
            if (SelectedAppLanguageIndex == -1)
            {
                return;
            }

            await _settingsActions.UpdateAppLanguageAsync(index, CancellationToken.None);
            UpdateShowAppLanguageRestartMessage();
        });
    }

    private void UpdateShowAppLanguageRestartMessage()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UpdateShowAppLanguageRestartMessage, () =>
        {
            ShowAppLanguageRestartMessage = 
                _localizationService.RequestedLanguage != _localizationService.StartupLanguage || 
                (_localizationService.LanguageOverride == null && _localizationService.StartupLanguage != _localizationService.DefaultLanguage);
        });
    }

    private void UpdateAppTheme(int index)
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UpdateAppTheme, () =>
        {
            SelectedAppThemeIndex = index;
            if (SelectedAppThemeIndex == -1)
            {
                return;
            }

            _settingsActions.UpdateAppTheme(index);
            UpdateShowAppThemeRestartMessage();
        });
    }

    private void UpdateShowAppThemeRestartMessage()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UpdateShowAppThemeRestartMessage, () =>
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
        });
    }

    private Task UpdateImageCaptureAutoSaveAsync(bool value)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoSave, async () =>
        {
            ImageCaptureAutoSave = value;
            await _settingsActions.UpdateImageAutoSaveAsync(value, CancellationToken.None);
        });
    }

    private Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoCopy, async () =>
        {
            ImageCaptureAutoCopy = value;
            await _settingsActions.UpdateImageAutoCopyAsync(value, CancellationToken.None);
        });
    }

    private Task ChangeScreenshotsFolderAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.ChangeScreenshotsFolder, async () =>
        {
            await _settingsActions.ChangeScreenshotsFolderAsync(CancellationToken.None);
            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _storageService.GetSystemDefaultScreenshotsFolderPath();
            }
            ScreenshotsFolderPath = screenshotsFolder;
        });
    }

    private void OpenScreenshotsFolder()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenScreenshotsFolder, () =>
        {
            _settingsActions.OpenScreenshotsFolder(ScreenshotsFolderPath);
        });
    }

    private void RestartApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, () => _settingsActions.RestartApp());
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () => 
        {
            _settingsActions.GoBack();
        });
    }

    private void ClearTemporaryFiles()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ClearTemporaryFiles, () =>
        {
            _settingsActions.ClearTemporaryFiles(TemporaryFilesFolderPath);
        });
    }

    private void OpenTemporaryFilesFolder()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenTemporaryFilesFolder, () =>
        {
            _settingsActions.OpenTemporaryFilesFolder();
        });
    }

    private Task RestoreDefaultSettingsAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.RestoreDefaultSettings, async () =>
        {
            await _settingsActions.RestoreDefaultSettingsAsync(CancellationToken.None);

            ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            ScreenshotsFolderPath = !string.IsNullOrEmpty(screenshotsFolder) ? screenshotsFolder : _storageService.GetSystemDefaultScreenshotsFolderPath();

            SelectedAppLanguageIndex = AppLanguages.Count - 1;
            SelectedAppThemeIndex = AppThemes.Count - 1;
        });
    }
}
