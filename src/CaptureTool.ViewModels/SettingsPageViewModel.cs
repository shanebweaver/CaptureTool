using CaptureTool.Common.Commands;
using CaptureTool.Core;
using CaptureTool.Core.AppController;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Telemetry;
using CaptureTool.Services.Themes;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : LoadableViewModelBase
{
    private readonly struct ActivityIds
    {
        public static readonly string Load = "SettingsPageViewModel_Load";
        public static readonly string Unload = "SettingsPageViewModel_Unload";
        public static readonly string RestartApp = "SettingsPageViewModel_RestartApp";
        public static readonly string UpdateAppLanguage = "SettingsPageViewModel_UpdateAppLanguage";
        public static readonly string UpdateAppTheme = "SettingsPageViewModel_UpdateAppTheme";
        public static readonly string UpdateShowAppThemeRestartMessage = "SettingsPageViewModel_UpdateShowAppThemeRestartMessage";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<AppLanguageViewModel, AppLanguage> _appLanguageViewModelFactory;
    private readonly IFactoryService<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public RelayCommand RestartAppCommand => new(RestartApp);
    public RelayCommand GoBackCommand => new(GoBack);
    public RelayCommand UpdateUseSystemCaptureOverlayCommand => new(UpdateUseSystemCaptureOverlay);

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

    private bool _useSystemCaptureOverlay;
    public bool UseSystemCaptureOverlay
    {
        get => _useSystemCaptureOverlay;
        set
        {
            Set(ref _useSystemCaptureOverlay, value);
            UpdateUseSystemCaptureOverlay();
        }
    }

    public SettingsPageViewModel(
        ITelemetryService telemetryService,
        IAppController appController,
        ILocalizationService localizationService,
        IThemeService themeService,
        ISettingsService settingsService,
        ICancellationService cancellationService,
        IFactoryService<AppLanguageViewModel, AppLanguage> appLanguageViewModelFactory,
        IFactoryService<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _telemetryService = telemetryService;
        _appController = appController;
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;
        _cancellationService = cancellationService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        _appThemes = [];
        _appLanguages = [];
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Debug.Assert(IsUnloaded);
        StartLoading();

        string activityId = ActivityIds.Load;
        _telemetryService.ActivityInitiated(activityId);

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            UseSystemCaptureOverlay = _settingsService.Get(CaptureToolSettings.UseSystemCaptureOverlay);

            AppTheme currentTheme = _themeService.CurrentTheme;

            // Languages
            AppLanguage[] languages = _localizationService.SupportedLanguages;
            for (var i = 0; i < languages.Length; i++)
            {
                AppLanguage language = languages[i];
                AppLanguageViewModel vm = _appLanguageViewModelFactory.Create(language);
                AppLanguages.Add(vm);

                if (language == _localizationService.CurrentLanguage)
                {
                    SelectedAppLanguageIndex = i;
                }
            }

            // Themes
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

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (OperationCanceledException)
        {
            _telemetryService.ActivityCanceled(activityId);
            throw;
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
            throw;
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        string activityId = ActivityIds.Unload;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            ShowAppLanguageRestartMessage = false;
            SelectedAppLanguageIndex = -1;
            AppLanguages.Clear();

            ShowAppThemeRestartMessage = false;
            SelectedAppThemeIndex = -1;
            AppThemes.Clear();

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }

        base.Unload();
    }

    private void UpdateUseSystemCaptureOverlay()
    {
        _settingsService.Set(CaptureToolSettings.UseSystemCaptureOverlay, UseSystemCaptureOverlay);
    }

    private void UpdateAppLanguage()
    {
        string activityId = ActivityIds.UpdateAppLanguage;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            if (SelectedAppLanguageIndex != -1)
            {
                AppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
                _localizationService.UpdateCurrentLanguage(vm.Language);
                ShowAppLanguageRestartMessage = vm.Language != _localizationService.StartupLanguage;
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void UpdateAppTheme()
    {
        string activityId = ActivityIds.UpdateAppLanguage;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            if (SelectedAppThemeIndex != -1)
            {
                AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
                _themeService.UpdateCurrentTheme(vm.AppTheme);
                UpdateShowAppThemeRestartMessage();
            }

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void UpdateShowAppThemeRestartMessage()
    {
        string activityId = ActivityIds.UpdateShowAppThemeRestartMessage;
        _telemetryService.ActivityInitiated(activityId);

        try
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

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void RestartApp()
    {
        string activityId = ActivityIds.RestartApp;
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            _appController.TryRestart();

            _telemetryService.ActivityCompleted(activityId);
        }
        catch (Exception e)
        {
            _telemetryService.ActivityError(activityId, e);
        }
    }

    private void GoBack()
    {
        _appController.GoBackOrHome();
    }
}
