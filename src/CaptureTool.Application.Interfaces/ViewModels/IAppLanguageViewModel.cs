using CaptureTool.Infrastructure.Interfaces.Localization;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IAppLanguageViewModel
{
    IAppLanguage? Language { get; }
    string DisplayName { get; }
    string AutomationName { get; }
}
