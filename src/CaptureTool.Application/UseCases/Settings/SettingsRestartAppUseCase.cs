using CaptureTool.Application.Abstractions.UseCases.Settings;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.UseCases.Settings;

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
