using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Presentation.ViewModels.Factories;

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
        return new AppLanguageViewModel(
            language,
            _localizationService);
    }
}
