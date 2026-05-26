using CaptureTool.Application.Abstractions.AppMenu;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.AppMenu;

internal class ExitApplicationAppCommand : IExitApplicationAppCommand
{
    private readonly IShutdownHandler _shutdownHandler;

    public ExitApplicationAppCommand(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute()
    {
        return !_shutdownHandler.IsShuttingDown;
    }

    public void Execute()
    {
        _shutdownHandler.Shutdown();
    }
}
