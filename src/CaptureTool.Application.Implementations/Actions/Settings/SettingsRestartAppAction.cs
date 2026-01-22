using CaptureTool.Common.Commands;
using CaptureTool.Application.Interfaces.Actions.Settings;
using CaptureTool.Infrastructure.Interfaces.Shutdown;

namespace CaptureTool.Application.Implementations.Actions.Settings;

public sealed partial class SettingsRestartAppAction : ActionCommand, ISettingsRestartAppAction
{
    private readonly IShutdownHandler _shutdownHandler;

    public SettingsRestartAppAction(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public override void Execute()
    {
        _shutdownHandler.TryRestart();
    }
}
