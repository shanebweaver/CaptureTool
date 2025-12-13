using CaptureTool.Core.Interfaces.Actions.Error;
using CaptureTool.Common.Commands.Extensions;

namespace CaptureTool.Core.Implementations.Actions.Error;

public sealed partial class ErrorActions : IErrorActions
{
    private readonly IErrorRestartAppAction _restartApp;

    public ErrorActions(IErrorRestartAppAction restartApp)
    {
        _restartApp = restartApp;
    }

    public void RestartApp() => _restartApp.ExecuteCommand();
}
