using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.About;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.About;

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
