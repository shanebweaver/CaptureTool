using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsGoBackUseCase : ActionCommand, ISettingsGoBackUseCase
{
    private readonly IAppNavigation _appNavigation;

    public SettingsGoBackUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
