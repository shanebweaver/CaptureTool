using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.UseCases.Error;
using CaptureTool.Infrastructure.Interfaces.Shutdown;

namespace CaptureTool.Application.Implementations.UseCases.Error;

public sealed partial class ErrorRestartAppUseCase : ActionCommand, IErrorRestartAppUseCase
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
