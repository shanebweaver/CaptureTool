using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Error;

internal sealed partial class ErrorRestartAppCommand : IErrorRestartAppCommand
{
    private readonly IShutdownHandler _shutdownHandler;

    public ErrorRestartAppCommand(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
