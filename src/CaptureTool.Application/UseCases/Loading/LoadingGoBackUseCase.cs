using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.Loading;
using CaptureTool.Infrastructure.UseCases;

namespace CaptureTool.Application.UseCases.Loading;

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
