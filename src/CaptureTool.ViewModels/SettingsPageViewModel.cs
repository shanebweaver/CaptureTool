using CaptureTool.Common.Commands;
using CaptureTool.Core.AppController;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
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
        public static readonly string GoBack = "SettingsPageViewModel_GoBack";
        public static readonly string UpdateAppLanguage = "SettingsPageViewModel_UpdateAppLanguage";
        public static readonly string UpdateAppTheme = "SettingsPageViewModel_UpdateAppTheme";
        public static readonly string UpdateShowAppThemeRestartMessage = "SettingsPageViewModel_UpdateShowAppThemeRestartMessage";
        public static readonly string UpdateShowAppLanguageRestartMessage = "SettingsPageViewModel_UpdateShowAppLanguageRestartMessage";
    }

    private readonly ITelemetryService _telemetryService;
    private readonly IAppController _appController;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<AppLanguageViewModel, AppLanguage?> _appLanguageViewModelFactory;
    private readonly IFactoryService<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public RelayCommand RestartAppCommand => new(RestartApp);
    public RelayCommand GoBackCommand => new(GoBack);

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

    public SettingsPageViewModel(
        ITelemetryService telemetryService,
        IAppController appController,
        ILocalizationService localizationService,
        IThemeService themeService,
        ICancellationService cancellationService,
        IFactoryService<AppLanguageViewModel, AppLanguage?> appLanguageViewModelFactory,
        IFactoryService<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _telemetryService = telemetryService;
        _appController = appController;
        _localizationService = localizationService;
        _themeService = themeService;
        _cancellationService = cancellationService;
        _appLanguageViewModelFactory = appLanguageViewModelFactory;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        _appThemes = [];
        _appLanguages = [];
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        ExecuteActivity(ActivityIds.Load, () =>
        {
            var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
            try
            {
                // Languages
                AppLanguage[] languages = _localizationService.SupportedLanguages;
                for (var i = 0; i < languages.Length; i++)
                {
                    AppLanguage language = languages[i];
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
            }
            finally
            {
                cts.Dispose();
            }
        });

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ExecuteActivity(ActivityIds.Unload, () =>
        {
            ShowAppLanguageRestartMessage = false;
            SelectedAppLanguageIndex = -1;
            AppLanguages.Clear();

            ShowAppThemeRestartMessage = false;
            SelectedAppThemeIndex = -1;
            AppThemes.Clear();
        });

        base.Unload();
    }

    private void UpdateAppLanguage()
    {
        ExecuteActivity(ActivityIds.UpdateAppLanguage, () =>
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
        ExecuteActivity(ActivityIds.UpdateShowAppLanguageRestartMessage, () =>
        {
            ShowAppLanguageRestartMessage = 
                _localizationService.RequestedLanguage != _localizationService.StartupLanguage || 
                (_localizationService.LanguageOverride == null && _localizationService.StartupLanguage != _localizationService.DefaultLanguage);
        });
    }

    private void UpdateAppTheme()
    {
        ExecuteActivity(ActivityIds.UpdateAppTheme, () =>
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
        ExecuteActivity(ActivityIds.UpdateShowAppThemeRestartMessage, () =>
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

    private void RestartApp()
    {
        ExecuteActivity(ActivityIds.RestartApp, () => _appController.TryRestart());
    }

    private void GoBack()
    {
        ExecuteActivity(ActivityIds.GoBack, () => _appController.GoBackOrHome());
    }

    private void ExecuteActivity(string activityId, Action activityAction)
    {
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            activityAction();
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
    }
}
