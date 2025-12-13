using CaptureTool.Common.Commands.Extensions;
using CaptureTool.Core.Interfaces.Actions.Loading;

namespace CaptureTool.Core.Implementations.Actions.Loading;

public sealed partial class LoadingActions : ILoadingActions
{
    private readonly ILoadingGoBackAction _goBack;

    public LoadingActions(ILoadingGoBackAction goBack)
    {
        _goBack = goBack;
    }

    public bool CanGoBack() => _goBack.CanExecute();
    public void GoBack() => _goBack.ExecuteCommand();
}
