using CaptureTool.Application.Abstractions.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;

public sealed class UpdateLoggingStateUseCase : IUpdateLoggingStateUseCase
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
                if (request.IsEnabled)
                {
                    _logService.Enable();
                }
                else
                {
                    _logService.Disable();
                }

                _settingsService.Set(CaptureToolSettings.VerboseLogging, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateLoggingStateResponse();
            },
            cancellationToken: cancellationToken);
    }
}
