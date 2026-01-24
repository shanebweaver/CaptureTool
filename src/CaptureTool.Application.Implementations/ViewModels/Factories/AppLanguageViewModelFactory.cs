using CaptureTool.Application.Interfaces.ViewModels;
using CaptureTool.Infrastructure.Interfaces.Factories;
using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Implementations.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryServiceWithArgs<IAppLanguageViewModel, IAppLanguage?>
{
    private readonly ILocalizationService _localizationService;
    public AppLanguageViewModelFactory(
        ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public IAppLanguageViewModel Create(IAppLanguage? language)
    {
        return new AppLanguageViewModel(
            language,
            _localizationService);
    }
}
