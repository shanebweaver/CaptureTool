using CaptureTool.Application.Abstractions.Messaging.Commands;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.UseCases.AppMenu;

internal class ExitApplication : IAppCommand
{
    private readonly IShutdownHandler _shutdownHandler;

    public ExitApplication(IShutdownHandler shutdownHandler)
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
