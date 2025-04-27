using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CaptureTool.Core;
using CaptureTool.FeatureManagement;
using CaptureTool.Services.Cancellation;
using CaptureTool.Services.Settings;
using CaptureTool.Services.Themes;
using Microsoft.Windows.Storage;

namespace CaptureTool.ViewModels;

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IThemeService _themeService;
    private readonly ICancellationService _cancellationService;
    private readonly IFeatureManager _featureManager;
    private readonly ISettingsService _settingsService;

    private readonly AppTheme[] SupportedAppThemes = [
        AppTheme.Light,
        AppTheme.Dark,
        AppTheme.SystemDefault,
    ];

    private ObservableCollection<AppTheme> _appThemeValues;
    public ObservableCollection<AppTheme> AppThemeValues
    {
        get => _appThemeValues;
        set => Set(ref _appThemeValues, value);
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
        IFeatureManager featureManager,
        ISettingsService settingsService)
    {
        _themeService = themeService;
        _cancellationService = cancellationService;
        _featureManager = featureManager;
        _settingsService = settingsService;

        _appThemeValues = [];
    }

    public override async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        Unload();
        Debug.Assert(IsUnloaded);
        StartLoading();

        var cts = _cancellationService.GetLinkedCancellationTokenSource(cancellationToken);
        try
        {
            foreach (AppTheme appTheme in SupportedAppThemes)
            {
                AppThemeValues.Add(appTheme);
            }

            SelectedAppThemeIndex = AppThemeValues.IndexOf(_themeService.CurrentTheme);
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
        base.Unload();
    }

    private void UpdateAppTheme()
    {
        if (SelectedAppThemeIndex != -1)
        {
            AppTheme appTheme = SupportedAppThemes[SelectedAppThemeIndex];
            _themeService.UpdateCurrentTheme(appTheme);
        }
    }
}
