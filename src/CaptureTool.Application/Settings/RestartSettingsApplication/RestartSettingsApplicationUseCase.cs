using CaptureTool.Application.Abstractions.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.RestartSettingsApplication;

internal sealed class RestartSettingsApplicationUseCase : IRestartSettingsApplicationUseCase
{
    private const string ActivityId = "RestartSettingsApplication";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IShutdownHandler _shutdownHandler;

    public RestartSettingsApplicationUseCase(IShutdownHandler shutdownHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _shutdownHandler = shutdownHandler;
    }

    public bool CanExecute(RestartSettingsApplicationRequest request) => !_shutdownHandler.IsShuttingDown;

    public Task<UseCaseResponse<RestartSettingsApplicationResponse>> ExecuteAsync(RestartSettingsApplicationRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool succeeded = _shutdownHandler.TryRestart();
                return new RestartSettingsApplicationResponse(succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
