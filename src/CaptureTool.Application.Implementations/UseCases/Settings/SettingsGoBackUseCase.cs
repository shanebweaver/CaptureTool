using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Application.Interfaces.Navigation;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

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
