using CaptureTool.Application.Abstractions.Features.Diagnostics.UpdateLoggingState;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.Diagnostics.UpdateLoggingState;

public sealed class UpdateLoggingStateUseCase : IUpdateLoggingStateUseCase
{
    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public UpdateLoggingStateUseCase(
        ILogService logService,
        ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    public async Task<UpdateLoggingStateResponse> ExecuteAsync(UpdateLoggingStateRequest request, CancellationToken cancellationToken = default)
    {
        try
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
        }
        catch (Exception)
        {
            return new UpdateLoggingStateResponse(false);
        }
    }
}
