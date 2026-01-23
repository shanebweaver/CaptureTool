using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.About;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.About;

public sealed partial class AboutGoBackUseCase : UseCase, IAboutGoBackUseCase
{
    private readonly IAppNavigation _appNavigation;

    public AboutGoBackUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
