using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Shutdown;

namespace CaptureTool.Application.Features.Settings.RestartSettingsApplication;

public sealed class RestartSettingsApplicationUseCase : IUseCase<RestartSettingsApplicationRequest, RestartSettingsApplicationResponse>, IConditional<RestartSettingsApplicationRequest>
{
    private readonly IShutdownHandler _shutdownHandler;

    public RestartSettingsApplicationUseCase(IShutdownHandler shutdownHandler)
    {
        _shutdownHandler = shutdownHandler;
    }

    public Task<bool> CanExecuteAsync(RestartSettingsApplicationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(!_shutdownHandler.IsShuttingDown);

    public Task<RestartSettingsApplicationResponse> ExecuteAsync(RestartSettingsApplicationRequest request, CancellationToken cancellationToken = default)
    {
        _shutdownHandler.TryRestart();
        return Task.FromResult(new RestartSettingsApplicationResponse());
    }
}