using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Infrastructure.Interfaces.Shutdown;

namespace CaptureTool.Core.Implementations.Actions.Error;

public sealed partial class ErrorRestartAppAction : ActionCommand, IErrorRestartAppAction
{
    private readonly IShutdownHandler _shutdownHandler;

    public ErrorRestartAppAction(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public override void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
