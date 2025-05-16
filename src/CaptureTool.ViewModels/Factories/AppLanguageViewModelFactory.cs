using CaptureTool.Services;
using CaptureTool.Services.Localization;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryService<AppLanguageViewModel, AppLanguage>
{
    public AppLanguageViewModel Create(AppLanguage language)
    {
        return new(language);
    }
}
