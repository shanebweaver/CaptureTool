using CaptureTool.Core.Interfaces.Actions.AddOns;
using CaptureTool.Common.Commands.Extensions;

namespace CaptureTool.Core.Implementations.Actions.AddOns;

public sealed partial class AddOnsActions : IAddOnsActions
{
    private readonly IAddOnsGoBackAction _goBack;

    public AddOnsActions(IAddOnsGoBackAction goBack)
    {
        _goBack = goBack;
    }

    public bool CanGoBack() => _goBack.CanGoBack();
    public void GoBack() => _goBack.GoBack();
}
