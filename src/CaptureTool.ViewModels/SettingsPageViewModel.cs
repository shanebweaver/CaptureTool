using CaptureTool.Common;
using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Core.Navigation;
using CaptureTool.Core.Settings;
using CaptureTool.Core.Telemetry;
using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Settings;
using CaptureTool.Services.Interfaces.Storage;
using CaptureTool.Services.Interfaces.Telemetry;
using CaptureTool.Services.Interfaces.Themes;
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
    }

    private readonly IAppNavigation _appNavigation;
    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IFilePickerService _filePickerService;
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

    private ObservableCollection<AppLanguageViewModel> _appLanguages;
    public ObservableCollection<AppLanguageViewModel> AppLanguages
    {
        get => _appLanguages;
        private set => Set(ref _appLanguages, value);
    }

    private int _selectedAppLanguageIndex;
    public int SelectedAppLanguageIndex
    {
        get => _selectedAppLanguageIndex;
        private set => Set(ref _selectedAppLanguageIndex, value);
    }

    private bool _showAppLanguageRestartMessage;
    public bool ShowAppLanguageRestartMessage
    {
        get => _showAppLanguageRestartMessage;
        private set => Set(ref _showAppLanguageRestartMessage, value);
    }

    private ObservableCollection<AppThemeViewModel> _appThemes;
    public ObservableCollection<AppThemeViewModel> AppThemes
    {
        get => _appThemes;
        private set => Set(ref _appThemes, value);
    }

    private int _selectedAppThemeIndex;
    public int SelectedAppThemeIndex
    {
        get => _selectedAppThemeIndex;
        private set => Set(ref _selectedAppThemeIndex, value);
    }

    private bool _showAppThemeRestartMessage;
    public bool ShowAppThemeRestartMessage
    {
        get => _showAppThemeRestartMessage;
        private set => Set(ref _showAppThemeRestartMessage, value);
    }

    private bool _imageCaptureAutoCopy;
    public bool ImageCaptureAutoCopy
    {
        get => _imageCaptureAutoCopy;
        private set => Set(ref _imageCaptureAutoCopy, value);
    }

    private bool _imageCaptureAutoSave;
    public bool ImageCaptureAutoSave
    {
        get => _imageCaptureAutoSave;
        private set => Set(ref _imageCaptureAutoSave, value);
    }

    private string _screenshotsFolderPath;
    public string ScreenshotsFolderPath
    {
        get => _screenshotsFolderPath;
        private set => Set(ref _screenshotsFolderPath, value);
    }

    public SettingsPageViewModel(
        IAppNavigation appNavigation,
        ITelemetryService telemetryService,
        IAppController appController,
        ILocalizationService localizationService,
        IThemeService themeService,
        IFilePickerService filePickerService,
        ISettingsService settingsService,
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
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        _appThemes = [];
        _appLanguages = [];
        _screenshotsFolderPath = string.Empty;

        ChangeScreenshotsFolderCommand = new(ChangeScreenshotsFolderAsync);
        OpenScreenshotsFolderCommand = new(OpenScreenshotsFolder);
        RestartAppCommand = new(RestartApp);
        GoBackCommand = new(GoBack);
        UpdateImageCaptureAutoCopyCommand = new(UpdateImageCaptureAutoCopyAsync);
        UpdateImageCaptureAutoSaveCommand = new(UpdateImageCaptureAutoSaveAsync);
        UpdateAppLanguageCommand = new(UpdateAppLanguageAsync);
        UpdateAppThemeCommand = new(UpdateAppTheme);
    }

    public override Task LoadAsync(CancellationToken cancellationToken)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.Load, async () =>
        {
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
                await UpdateAppLanguageAsync(appLanguageIndex);
            }
            else
            {
                await UpdateAppLanguageAsync(AppLanguages.Count - 1);
            }
            UpdateShowAppLanguageRestartMessage();

            // Themes
            AppTheme currentTheme = _themeService.CurrentTheme;
            int appThemeIndex = -1;
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
                UpdateAppTheme(appThemeIndex);
            }
            else
            {
                UpdateAppTheme(SupportedAppThemes.IndexOf(AppTheme.SystemDefault));
            }
            UpdateShowAppThemeRestartMessage();

            await UpdateImageCaptureAutoCopyAsync(_settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoCopy));
            await UpdateImageCaptureAutoSaveAsync(_settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSave));

            var screenshotsFolder = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_ScreenshotsFolder);
            if (string.IsNullOrWhiteSpace(screenshotsFolder))
            {
                screenshotsFolder = _appController.GetDefaultScreenshotsFolderPath();
            }

            ScreenshotsFolderPath = screenshotsFolder;

            await base.LoadAsync(cancellationToken);
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


    private Task UpdateAppLanguageAsync(int index)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateAppLanguage, async () =>
        {
            SelectedAppLanguageIndex = index;
            if (SelectedAppLanguageIndex == -1)
            {
                return;
            }

            if (IsLoading)
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
        
            if (IsLoading)
            {
                return;
            }

            AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
            if (vm.AppTheme == _themeService.CurrentTheme)
            {
                return;
            }

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

            if (IsLoading)
            {
                return;
            }
           
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoSave, ImageCaptureAutoSave);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private Task UpdateImageCaptureAutoCopyAsync(bool value)
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.UpdateImageCaptureAutoCopy, async () =>
        {
            ImageCaptureAutoCopy = value;

            if (IsLoading)
            {

            }
           
            _settingsService.Set(CaptureToolSettings.Settings_ImageCapture_AutoCopy, ImageCaptureAutoCopy);
            await _settingsService.TrySaveAsync(CancellationToken.None);
        });
    }

    private Task ChangeScreenshotsFolderAsync()
    {
        return TelemetryHelper.ExecuteActivityAsync(_telemetryService, ActivityIds.ChangeScreenshotsFolder, async () =>
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
