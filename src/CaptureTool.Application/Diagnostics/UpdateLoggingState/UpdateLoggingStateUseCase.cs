using CaptureTool.Application.Abstractions.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Diagnostics.UpdateLoggingState;

internal sealed class UpdateLoggingStateUseCase : IUpdateLoggingStateUseCase
{
    private const string ActivityId = "UpdateLoggingState";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public UpdateLoggingStateUseCase(ILogService logService,
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _logService = logService;
        _settingsService = settingsService;
    }

    public Task<UseCaseResponse<UpdateLoggingStateResponse>> ExecuteAsync(UpdateLoggingStateRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.VerboseLogging,
                    request.IsEnabled,
                    cancellationToken);
                if (!result.Succeeded)
                {
                    return new UpdateLoggingStateResponse(false);
                }

                if (request.IsEnabled)
                {
                    _logService.Enable();
                }
                else
                {
                    _logService.Disable();
                }

                return new UpdateLoggingStateResponse();
            },
            cancellationToken: cancellationToken);
    }
}
