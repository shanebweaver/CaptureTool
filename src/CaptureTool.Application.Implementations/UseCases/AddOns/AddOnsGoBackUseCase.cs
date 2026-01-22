using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.AddOns;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.AddOns;

public sealed partial class AddOnsGoBackUseCase : ActionCommand, IAddOnsGoBackUseCase
{
    private readonly IAppNavigation _appNavigation;

    public AddOnsGoBackUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
