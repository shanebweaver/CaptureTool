using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Services;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Themes;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ICancellationService _cancellationService;
    private readonly IFactoryService<AppThemeViewModel, AppTheme> _appThemeViewModelFactory;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

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

    public SettingsPageViewModel(
        IThemeService themeService,
        ICancellationService cancellationService,
        IFactoryService<AppThemeViewModel, AppTheme> appThemeViewModelFactory)
    {
        _themeService = themeService;
        _cancellationService = cancellationService;
        _appThemeViewModelFactory = appThemeViewModelFactory;

        _appThemes = [];
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

            for(var i = 0; i < SupportedAppThemes.Length; i++)
            {
                AppTheme supportedTheme = SupportedAppThemes[i];
                AppThemeViewModel vm = _appThemeViewModelFactory.Create(supportedTheme);
                AppThemes.Add(vm);

                if (supportedTheme == currentTheme)
                {
                    SelectedAppThemeIndex = i;
                }
            }
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
        SelectedAppThemeIndex = -1;
        AppThemes.Clear();
        base.Unload();
    }

    private void UpdateAppTheme()
    {
        if (SelectedAppThemeIndex != -1)
        {
            AppThemeViewModel vm = AppThemes[SelectedAppThemeIndex];
            _themeService.UpdateCurrentTheme(vm.AppTheme);
        }
    }
}
