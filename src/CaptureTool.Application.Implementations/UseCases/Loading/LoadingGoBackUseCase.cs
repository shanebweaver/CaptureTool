using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Loading;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.Loading;

public sealed partial class LoadingGoBackUseCase : UseCase, ILoadingGoBackUseCase
{
    private readonly IAppNavigation _appNavigation;

    public LoadingGoBackUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
