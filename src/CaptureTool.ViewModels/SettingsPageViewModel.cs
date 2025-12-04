using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Settings;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Shutdown;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using CaptureTool.Services.Interfaces.Windowing;
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

    private readonly IAppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;
    private readonly IShutdownHandler _shutdownHandler;
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
        IAppNavigation appNavigation,
        ITelemetryService telemetryService,
        IShutdownHandler shutdownHandler,
        IWindowHandleProvider windowingService,
        ILocalizationService localizationService,
        IThemeService themeService,
        IFilePickerService filePickerService,
        ISettingsService settingsService,
        IStorageService storageService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;
        _shutdownHandler = shutdownHandler;
        _windowingService = windowingService;
        _localizationService = localizationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _filePickerService = filePickerService;
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

            AppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
            if (vm.Language == _localizationService.LanguageOverride)
            {
                return;
            }

            _localizationService.OverrideLanguage(vm.Language);

            if (vm.Language?.Value is string language)
            {
                _settingsService.Set(CaptureToolSettings.Settings_LanguageOverride, vm.Language.Value);
            }
            else
            {
                _settingsService.Unset(CaptureToolSettings.Settings_LanguageOverride);
            }

            await _settingsService.TrySaveAsync(CancellationToken.None);
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

            AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
            _themeService.UpdateCurrentTheme(vm.AppTheme);
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
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, ImageCaptureAutoSave);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoCopy, async () =>
        {
            ImageCaptureAutoCopy = value;
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, ImageCaptureAutoCopy);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private Task ChangeScreenshotsFolderAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.ChangeScreenshotsFolder, async () =>
        {
            var hwnd = _windowingService.GetMainWindowHandle();
            IFolder folder = await _filePickerService.PickFolderAsync(hwnd, UserFolder.Pictures)
                ?? throw new OperationCanceledException("No folder was selected.");

            ScreenshotsFolderPath = folder.FolderPath;

            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder, folder.FolderPath);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private void OpenScreenshotsFolder()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenScreenshotsFolder, () =>
        {
            if (Directory.Exists(ScreenshotsFolderPath))
            {
                Process.Start("explorer.exe", $"/open, {ScreenshotsFolderPath}");
            }
            else
            {
                throw new DirectoryNotFoundException($"The screenshots folder path '{ScreenshotsFolderPath}' does not exist.");
            }
        });
    }

    private void RestartApp()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, () => _shutdownHandler.TryRestart());
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () => 
        {
            _appNavigation.GoBackOrGoHome();
        });
    }

    private void ClearTemporaryFiles()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.ClearTemporaryFiles, () =>
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(TemporaryFilesFolderPath))
            {
                try
                {
                    if (Directory.Exists(entry))
                    {
                        Directory.Delete(entry, true);
                    }
                    else
                    {
                        File.Delete(entry);
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }
        });
    }

    private void OpenTemporaryFilesFolder()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.OpenTemporaryFilesFolder, () =>
        {
            if (Directory.Exists(TemporaryFilesFolderPath))
            {
                Process.Start("explorer.exe", $"/open, {TemporaryFilesFolderPath}");
            }
            else
            {
                throw new DirectoryNotFoundException($"The temporary folder path '{TemporaryFilesFolderPath}' does not exist.");
            }
        });
    }

    private Task RestoreDefaultSettingsAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.RestoreDefaultSettings, async () =>
        {
            _settingsService.ClearAllSettings();
            await _settingsService.TrySaveAsync(CancellationToken.None);

            ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            ScreenshotsFolderPath = !string.IsNullOrEmpty(screenshotsFolder) ? screenshotsFolder : _storageService.GetSystemDefaultScreenshotsFolderPath();

            SelectedAppLanguageIndex = AppLanguages.Count - 1;
            SelectedAppThemeIndex = AppThemes.Count - 1;
        });
    }
}
