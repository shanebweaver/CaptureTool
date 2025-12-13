using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.About;
using CaptureTool.Core.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.About;

public sealed partial class AboutGoBackAction : ActionCommand, IAboutGoBackAction
{
    private readonly IAppNavigation _appNavigation;

    public AboutGoBackAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public bool CanGoBack() => _appNavigation.CanGoBack;

    public void GoBack() => Execute();

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
