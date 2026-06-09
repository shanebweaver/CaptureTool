using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Application.Abstractions.Themes;
using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.Settings.Factories;

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
        return new AppThemeViewModel(
            appTheme,
            _localizationService);
    }
}
