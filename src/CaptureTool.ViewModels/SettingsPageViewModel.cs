using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Themes;
using CaptureTool.ViewModels.Commands;
using CaptureTool.ViewModels.Factories;
using Microsoft.Windows.Globalization;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IAppController _appController;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<AppLanguageViewModel, string> _appLanguageViewModelFactory;
    private readonly IFactoryService<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    public RelayCommand RestartAppCommand => new(RestartApp);

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
        IAppController appController,
        ILocalizationService localizationService,
        IThemeService themeService,
        ICancellationService cancellationService,
        IFactoryService<AppLanguageViewModel, string> appLanguageViewModelFactory,
        IFactoryService<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
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

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            AppTheme currentTheme = _themeService.CurrentTheme;

            // Languages
            string[] languages = _localizationService.SupportedLanguages;
            for (var i = 0; i < languages.Length; i++)
            {
                string language = languages[i];
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
        }
        catch (OperationCanceledException)
        {
            // Load canceled
        }
        finally
        {
            cts.Dispose();
        }

        await base.LoadAsync(parameter, cancellationToken);
    }

    public override void Unload()
    {
        ShowAppLanguageRestartMessage = false;
        SelectedAppLanguageIndex = -1;
        AppLanguages.Clear();

        ShowAppThemeRestartMessage = false;
        SelectedAppThemeIndex = -1;
        AppThemes.Clear();

        base.Unload();
    }

    private void UpdateAppLanguage()
    {
        if (SelectedAppLanguageIndex != -1)
        {
            AppLanguageViewModel vm = AppLanguages[SelectedAppLanguageIndex];
            if (vm.Language != null)
            {
                _localizationService.UpdateCurrentLanguage(vm.Language);
                ShowAppLanguageRestartMessage = vm.Language != _localizationService.StartupLanguage;
            }
        }
    }

    private void UpdateAppTheme()
    {
        if (SelectedAppThemeIndex != -1)
        {
            AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
            _themeService.UpdateCurrentTheme(vm.AppTheme);
            UpdateShowAppThemeRestartMessage();
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

    private void RestartApp()
    {
        _appController.TryRestart();
    }
}
