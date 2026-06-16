using CaptureTool.Application.Abstractions.Features.Settings.RestartSettingsApplication;
using CaptureTool.Application.Abstractions.Shutdown;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.RestartSettingsApplication;

public sealed class RestartSettingsApplicationUseCase : IRestartSettingsApplicationUseCase
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
                _shutdownHandler.TryRestart();
                return new RestartSettingsApplicationResponse();
            },
            cancellationToken: cancellationToken);
    }
}
