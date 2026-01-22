using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.About;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.Actions.About;

public sealed partial class AboutGoBackAction : ActionCommand, IAboutGoBackAction
{
    private readonly IAppNavigation _appNavigation;

    public AboutGoBackAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
