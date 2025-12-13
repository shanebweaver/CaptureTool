using CaptureTool.Core.Interfaces.Actions.About;
using CaptureTool.Common.Commands.Extensions;

namespace CaptureTool.Core.Implementations.Actions.About;

public sealed partial class AboutActions : IAboutActions
{
    private readonly IAboutGoBackAction _goBack;

    public AboutActions(IAboutGoBackAction goBack)
    {
        _goBack = goBack;
    }

    public bool CanGoBack() => _goBack.CanExecute();
    public void GoBack() => _goBack.ExecuteCommand();
}
