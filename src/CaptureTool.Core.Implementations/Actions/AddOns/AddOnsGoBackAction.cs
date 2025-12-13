using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Core.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.AddOns;

public sealed partial class AddOnsGoBackAction : ActionCommand, IAddOnsGoBackAction
{
    private readonly IAppNavigation _appNavigation;

    public AddOnsGoBackAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
