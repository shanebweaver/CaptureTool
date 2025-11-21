using CaptureTool.Services.Interfaces;
using CaptureTool.Services.Interfaces.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryServiceWithArgs<AppLanguageViewModel, IAppLanguage?>
{
    private readonly ILocalizationService _localizationService;
    public AppLanguageViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public AppLanguageViewModel Create(IAppLanguage? language)
    {
        return new(
            language, 
            _localizationService);
    }
}