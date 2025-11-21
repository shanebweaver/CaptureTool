using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;
using CaptureTool.Services.Interfaces.Themes;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppThemeViewModelFactory : IFactoryServiceWithArgs<AppThemeViewModel, AppTheme>
{
    private readonly ILocalizationService _localizationService;

    public AppThemeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public AppThemeViewModel Create(AppTheme appTheme)
    {
        return new(
            appTheme,
            _localizationService);
    }
}
