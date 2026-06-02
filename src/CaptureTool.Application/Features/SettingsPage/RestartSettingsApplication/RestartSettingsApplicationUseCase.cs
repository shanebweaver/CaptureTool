using CaptureTool.Application.Abstractions.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.Settings.RestartSettingsApplication;

public sealed class RestartSettingsApplicationUseCase : IRestartSettingsApplicationUseCase
{
    private readonly IShutdownHandler _shutdownHandler;

    public RestartSettingsApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(RestartSettingsApplicationRequest request) => !_shutdownHandler.IsShuttingDown;

    public Task<RestartSettingsApplicationResponse> ExecuteAsync(RestartSettingsApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.TryRestart();
        return Task.FromResult(new RestartSettingsApplicationResponse());
    }
}