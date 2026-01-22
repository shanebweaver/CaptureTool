using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Loading;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.Actions.Loading;

public sealed partial class LoadingGoBackAction : ActionCommand, ILoadingGoBackAction
{
    private readonly IAppNavigation _appNavigation;

    public LoadingGoBackAction(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
