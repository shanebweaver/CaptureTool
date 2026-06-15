using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.Telemetry;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.Diagnostics.ExportLogs;

public sealed class ExportLogsUseCase : IExportLogsUseCase
{
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly ITelemetryService _telemetryService;

    public ExportLogsUseCase(
        IFilePickerService filePickerService,
        ILogService logService,
        ITelemetryService telemetryService)
    {
        _filePickerService = filePickerService;
        _logService = logService;
        _telemetryService = telemetryService;
    }

    public async Task<ExportLogsResponse> ExecuteAsync(ExportLogsRequest request, CancellationToken cancellationToken = default)
    {
        const string activityId = nameof(ExportLogsUseCase);

        try
        {
            IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents);
            if (file is null || cancellationToken.IsCancellationRequested)
            {
                return new ExportLogsResponse(false);
            }

            _telemetryService.ActivityInitiated(activityId);

            string logs = string.Join(Environment.NewLine, _logService.GetLogs().Select(log => log.ToString()));
            await File.WriteAllTextAsync(file.FilePath, logs, cancellationToken);

            _telemetryService.ActivityCompleted(activityId, file.FilePath);
            return new ExportLogsResponse(true);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(activityId, exception.Message);
            return new ExportLogsResponse(false);
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(activityId, exception);
            return new ExportLogsResponse(false);
        }
    }
}
