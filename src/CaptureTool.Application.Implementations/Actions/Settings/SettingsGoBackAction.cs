using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsGoBackAction : ActionCommand, ISettingsGoBackAction
{
    private readonly IAppNavigation _appNavigation;

    public SettingsGoBackAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
