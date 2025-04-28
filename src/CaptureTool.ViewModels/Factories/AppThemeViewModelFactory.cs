using CaptureTool.Services;
using CaptureTool.Services.Localization;
using CaptureTool.Services.Themes;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppThemeViewModelFactory : IFactoryService<AppThemeViewModel, AppTheme>
{
    private readonly ILocalizationService _localizationService;

    public AppThemeViewModelFactory(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public AppThemeViewModel Create(AppTheme appTheme)
    {
        return new(_localizationService)
        {
            AppTheme = appTheme
        };
    }
}
