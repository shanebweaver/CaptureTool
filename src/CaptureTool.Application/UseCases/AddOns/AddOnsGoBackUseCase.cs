using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.AddOns;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.AddOns;

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
