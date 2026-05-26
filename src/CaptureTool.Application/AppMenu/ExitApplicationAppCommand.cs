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

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return true;
    }

    public void Execute()
    {
        _shutdownHandler.Shutdown();
    }
}
