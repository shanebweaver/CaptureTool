using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.AddOns;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.Actions.AddOns;

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
