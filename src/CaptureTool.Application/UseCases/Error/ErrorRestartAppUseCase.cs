using CaptureTool.Application.Abstractions.UseCases.Error;
using CaptureTool.Infrastructure.UseCases;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.UseCases.Error;

public sealed partial class ErrorRestartAppUseCase : UseCase, IErrorRestartAppUseCase
{
    private readonly IShutdownHandler _shutdownHandler;

    public ErrorRestartAppUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public override void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
