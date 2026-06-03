using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Presentation.Factories;

namespace CaptureTool.Presentation.Features.Settings.Factories;

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
