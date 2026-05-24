using CaptureTool.Application.Abstractions.Navigation;
using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.Implementations.UseCases;

namespace CaptureTool.Application.UseCases.Settings;

public sealed partial class SettingsGoBackUseCase : UseCase, ISettingsGoBackUseCase
{
    private readonly IAppNavigation _appNavigation;

    public SettingsGoBackUseCase(IAppNavigation appNavigation)
    {
        _appNavigation = appNavigation;
    }

    public override void Execute()
    {
        _appNavigation.GoBackOrGoHome();
    }
}
