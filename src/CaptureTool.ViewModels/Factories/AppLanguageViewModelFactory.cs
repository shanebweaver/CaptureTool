using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryService<AppLanguageViewModel, AppLanguage?>
{
    private readonly ILocalizationService _localizationService;
    public AppLanguageViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public AppLanguageViewModel Create(AppLanguage? language)
    {
        return new(
            language, 
            _localizationService);
    }
}
