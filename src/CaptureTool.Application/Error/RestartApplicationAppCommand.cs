using CaptureTool.Application.Abstractions.Error;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Error;

internal class RestartApplicationAppCommand : IRestartApplicationAppCommand
{
    private readonly IShutdownHandler _shutdownHandler;

    public RestartApplicationAppCommand(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute()
    {
        return !_shutdownHandler.IsShuttingDown;
    }

    public void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
