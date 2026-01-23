using CaptureTool.Infrastructure.Implementations.UseCases;
using CaptureTool.Infrastructure.Interfaces.UseCases;
using CaptureTool.Application.Interfaces.UseCases.Settings;
using CaptureTool.Infrastructure.Interfaces.Shutdown;

namespace CaptureTool.Application.Implementations.UseCases.Settings;

public sealed partial class SettingsRestartAppUseCase : UseCase, ISettingsRestartAppUseCase
{
    private readonly IShutdownHandler _shutdownHandler;

    public SettingsRestartAppUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public override void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
