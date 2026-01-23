using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces;
using CaptureTool.Infrastructure.Interfaces.Localization;
using CaptureTool.Infrastructure.Interfaces.Themes;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

public sealed partial class AppThemeViewModelFactory : IFactoryServiceWithArgs<IAppThemeViewModel, AppTheme>
{
    private readonly ILocalizationService _localizationService;

    public AppThemeViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public IAppThemeViewModel Create(AppTheme appTheme)
    {
        return new AppThemeViewModel(
            appTheme,
            _localizationService);
    }
}
