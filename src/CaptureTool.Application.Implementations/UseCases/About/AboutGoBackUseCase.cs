using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.About;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.About;

public sealed partial class AboutGoBackUseCase : ActionCommand, IAboutGoBackUseCase
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
