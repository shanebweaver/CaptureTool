using CaptureTool.Common.Commands;
using CaptureTool.Core.Interfaces.Actions.Settings;
using CaptureTool.Services.Interfaces.Shutdown;

namespace CaptureTool.Core.Implementations.Actions.Settings;

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
