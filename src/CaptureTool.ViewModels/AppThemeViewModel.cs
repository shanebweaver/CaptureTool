using System;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Themes;

namespace CaptureTool.ViewModels;

public sealed partial class AppThemeViewModel : ViewModelBase
{
    private readonly ILocalizationService _localizationService;

    private AppTheme _appTheme;
    public AppTheme AppTheme
    {
        get => _appTheme;
        set
        {
            Set(ref _appTheme, value);
            UpdateDisplayName();
        }
    }

    private string? _displayName;
    public string? DisplayName
    {
        get => _displayName;
        set => Set(ref _displayName, value);
    }

    public AppThemeViewModel(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    private void UpdateDisplayName()
    {
        DisplayName = _localizationService.GetString($"AppTheme_{Enum.GetName(AppTheme)}");
    }
}
