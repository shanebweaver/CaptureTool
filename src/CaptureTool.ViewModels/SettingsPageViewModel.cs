using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Settings;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Cancellation;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "SettingsPageViewModel_Load";
        public static readonly string Dispose = "SettingsPageViewModel_Dispose";
        public static readonly string RestartApp = "SettingsPageViewModel_RestartApp";
        public static readonly string GoBack = "SettingsPageViewModel_GoBack";
        public static readonly string UpdateImageCaptureAutoCopy = "SettingsPageViewModel_UpdateImageCaptureAutoCopy";
        public static readonly string UpdateImageCaptureAutoSave = "SettingsPageViewModel_UpdateImageCaptureAutoSave";
        public static readonly string ChangeScreenshotsFolder = "SettingsPageViewModel_ChangeScreenshotsFolder";
        public static readonly string OpenScreenshotsFolder = "SettingsPageViewModel_OpenScreenshotsFolder";
        public static readonly string UpdateAppLanguage = "SettingsPageViewModel_UpdateAppLanguage";
        public static readonly string UpdateAppTheme = "SettingsPageViewModel_UpdateAppTheme";
        public static readonly string UpdateShowAppThemeRestartMessage = "SettingsPageViewModel_UpdateShowAppThemeRestartMessage";
        public static readonly string UpdateShowAppLanguageRestartMessage = "SettingsPageViewModel_UpdateShowAppLanguageRestartMessage";
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IFilePickerService _filePickerService;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public RelayCommand ChangeScreenshotsFolderCommand { get; }
    public RelayCommand OpenScreenshotsFolderCommand { get; }
    public RelayCommand RestartAppCommand { get; }
    public RelayCommand GoBackCommand { get; }

    private ObservableCollection<AppLanguageViewModel> _appLanguages;
    public ObservableCollection<AppLanguageViewModel> AppLanguages
    {
        get => _appLanguages;
        set => Set(ref _appLanguages, value);
    }

    private int _selectedAppLanguageIndex;
    public int SelectedAppLanguageIndex
    {
        get => _selectedAppLanguageIndex;
        set
        {
            Set(ref _selectedAppLanguageIndex, value);
            UpdateAppLanguage();
        }
    }

    private bool _showAppLanguageRestartMessage;
    public bool ShowAppLanguageRestartMessage
    {
        get => _showAppLanguageRestartMessage;
        set => Set(ref _showAppLanguageRestartMessage, value);
    }

    private ObservableCollection<AppThemeViewModel> _appThemes;
    public ObservableCollection<AppThemeViewModel> AppThemes
    {
        get => _appThemes;
        set => Set(ref _appThemes, value);
    }

    private int _selectedAppThemeIndex;
    public int SelectedAppThemeIndex
    {
        get => _selectedAppThemeIndex;
        set
        {
            Set(ref _selectedAppThemeIndex, value);
            UpdateAppTheme();
        }
    }

    private bool _showAppThemeRestartMessage;
    public bool ShowAppThemeRestartMessage
    {
        get => _showAppThemeRestartMessage;
        set => Set(ref _showAppThemeRestartMessage, value);
    }

    private bool _imageCaptureAutoCopy;
    public bool ImageCaptureAutoCopy
    {
        get => _imageCaptureAutoCopy;
        set
        {
            Set(ref _imageCaptureAutoCopy, value);
            UpdateImageCaptureAutoCopy();
        }
    }

    private bool _imageCaptureAutoSave;
    public bool ImageCaptureAutoSave
    {
        get => _imageCaptureAutoSave;
        set
        {
            Set(ref _imageCaptureAutoSave, value);
            UpdateImageCaptureAutoSave();
        }
    }

    private string _screenshotsFolderPath;
    public string ScreenshotsFolderPath
    {
        get => _screenshotsFolderPath;
        set => Set(ref _screenshotsFolderPath, value);
    }

    public SettingsPageViewModel(
        IAppNavigation appNavigation,
        ITelemetryService telemetryService,
        IAppController appController,
        ILocalizationService localizationService,
        IThemeService themeService,
        IFilePickerService filePickerService,
        ISettingsService settingsService,
        ICancellationService cancellationService,
        IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?> appLanguageViewModelFactory,
        IFactoryServiceWithArgs<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _appNavigation = appNavigation;
        _telemetryService = telemetryService;
        _appController = appController;
        _localizationService = localizationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _filePickerService = filePickerService;
        _cancellationService = cancellationService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        _appThemes = [];
        _appLanguages = [];
        _screenshotsFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = new(ChangeScreenshotsFolder);
        OpenScreenshotsFolderCommand = new(OpenScreenshotsFolder);
        RestartAppCommand = new(RestartApp);
        GoBackCommand = new(GoBack);
    }

    public override void Load()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.Load, () =>
        {
            // Languages
            IAppLanguage[] languages = _localizationService.SupportedLanguages;
            for (var i = 0; i < languages.Length; i++)
            {
                IAppLanguage language = languages[i];
                AppLanguageViewModel vm = _appLanguageViewModelFactory.Create(language);
                AppLanguages.Add(vm);

                if (language.Value == _localizationService.LanguageOverride?.Value)
                {
                    SelectedAppLanguageIndex = i;
                }
            }
            AppLanguages.Add(_appLanguageViewModelFactory.Create(null)); // Null for system default
            if (SelectedAppLanguageIndex == -1)
            {
                SelectedAppLanguageIndex = AppLanguages.Count - 1;
            }
            UpdateShowAppLanguageRestartMessage();

            // Themes
            AppTheme currentTheme = _themeService.CurrentTheme;
            for (var i = 0; i < SupportedAppThemes.Length; i++)
            {
                AppTheme supportedTheme = SupportedAppThemes[i];
                AppThemeViewModel vm = _appThemeViewModelFactory.Create(supportedTheme);
                AppThemes.Add(vm);

                if (supportedTheme == currentTheme)
                {
                    SelectedAppThemeIndex = i;
                }
            }
            UpdateShowAppThemeRestartMessage();

            ImageCaptureAutoCopy = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy);
            ImageCaptureAutoSave = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave);

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _appController.GetDefaultScreenshotsFolderPath();
            }

            ScreenshotsFolderPath = screenshotsFolder;

            base.Load();
        });
    }

    public override void Dispose()
    {
        _showAppLanguageRestartMessage = false;
        _selectedAppLanguageIndex = -1;
        _appLanguages.Clear();

        _showAppThemeRestartMessage = false;
        _selectedAppThemeIndex = -1;
        _appThemes.Clear();

        _imageCaptureAutoSave = false;
        _imageCaptureAutoCopy = false;

        base.Dispose();
    }

    private void UpdateAppLanguage()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UpdateAppLanguage, () =>
        {
            if (SelectedAppLanguageIndex != -1)
            {
                AppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
                if (vm.Language != _localizationService.LanguageOverride)
                {
                    _localizationService.OverrideLanguage(vm.Language);
                    UpdateShowAppLanguageRestartMessage();
                }
            }
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

    private void UpdateAppTheme()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.UpdateAppTheme, () =>
        {
            if (SelectedAppThemeIndex != -1)
            {
                AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
                if (vm.AppTheme != _themeService.CurrentTheme)
                {
                    _themeService.UpdateCurrentTheme(vm.AppTheme);
                    UpdateShowAppThemeRestartMessage();
                }
            }
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

    private async void UpdateImageCaptureAutoSave()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoSave, async () =>
        {
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, ImageCaptureAutoSave);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private async void UpdateImageCaptureAutoCopy()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoCopy, async () =>
        {
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, ImageCaptureAutoCopy);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private async void ChangeScreenshotsFolder()
    {
        await TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.ChangeScreenshotsFolder, async () =>
        {
            var hwnd = _appController.GetMainWindowHandle();
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
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.RestartApp, () => _appController.TryRestart());
    }

    private void GoBack()
    {
        TelemetryHelper.ExecuteActivity(_telemetryService, ActivityIds.GoBack, () => 
        {
            _appNavigation.GoBackOrGoHome();
        });
    }
}
