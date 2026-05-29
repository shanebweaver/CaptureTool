using CaptureTool.Infrastructure.Abstractions.Factories;
using CaptureTool.Infrastructure.Abstractions.Localization;
using CaptureTool.Infrastructure.Abstractions.Themes;

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
