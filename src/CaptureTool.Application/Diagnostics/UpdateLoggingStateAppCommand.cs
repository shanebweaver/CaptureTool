using CaptureTool.Application.Abstractions.Diagnostics;
using CaptureTool.Application.Settings;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Diagnostics;

internal class UpdateLoggingStateAppCommand : IUpdateLoggingStateAppCommand
{
    public UpdateLoggingStateAppCommand(
        ILogService logService,
        ISettingsService settingsService)
    {
        _logService = logService;
        _settingsService = settingsService;
    }

    private readonly ILogService _logService;
    private readonly ISettingsService _settingsService;

    public bool IsExecuting { get; protected set; }

    public async Task ExecuteAsync(bool parameter, CancellationToken cancellationToken)
    {
        IsExecuting = true;

        try
        {
            if (parameter)
            {
                _logService.Enable();
            }
            else
            {
                _logService.Disable();
            }

            _settingsService.Set(CaptureToolSettings.VerboseLogging, parameter);
            await _settingsService.TrySaveAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }
}
