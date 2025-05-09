using CaptureTool.Services;

namespace CaptureTool.ViewModels.Factories;

public sealed partial class AppLanguageViewModelFactory : IFactoryService<AppLanguageViewModel, string>
{
    public AppLanguageViewModelFactory()
    {
    }

    public AppLanguageViewModel Create(string language)
    {
        return new()
        {
            Language = language
        };
    }
}
