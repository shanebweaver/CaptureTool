using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Loading;
using CaptureTool.Core.Interfaces.Navigation;

namespace CaptureTool.Core.Implementations.Actions.Loading;

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
