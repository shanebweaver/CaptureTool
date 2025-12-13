using CaptureTool.Core.Interfaces.Actions.About;

namespace CaptureTool.Core.Implementations.Actions.About;

public sealed partial class AboutActions : IAboutActions
{
    private readonly IAboutGoBackAction _goBack;

    public AboutActions(IAboutGoBackAction goBack)
    {
        _goBack = goBack;
    }

    public bool CanGoBack() => _goBack.CanGoBack();
    public void GoBack() => _goBack.GoBack();
}
