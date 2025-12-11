using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Core.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.Settings;

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
