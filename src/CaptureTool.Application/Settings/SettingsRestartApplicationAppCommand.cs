using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Settings;

public sealed partial class SettingsRestartApplicationAppCommand : ISettingsRestartApplicationAppCommand
{
    private readonly IShutdownHandler _shutdownHandler;

    public SettingsRestartApplicationAppCommand(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute()
    {
        return !_shutdownHandler.IsShuttingDown;
    }

    public void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
