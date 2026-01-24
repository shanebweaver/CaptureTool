using CaptureTool.Application.Interfaces.Navigation;
using CaptureTool.Application.Interfaces.UseCases.AddOns;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.Implementations.UseCases.AddOns;

public sealed partial class AddOnsGoBackUseCase : UseCase, IAddOnsGoBackUseCase
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
