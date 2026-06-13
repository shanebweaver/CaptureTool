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
        IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents);
        if (file is null)
        {
            return new ExportLogsResponse(false);
        }

        const string activityId = nameof(ExportLogsUseCase);
        _telemetryService.ActivityInitiated(activityId);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string logs = string.Join(Environment.NewLine, _logService.GetLogs().Select(log => log.ToString()));
            await File.WriteAllTextAsync(file.FilePath, logs, cancellationToken);

            _telemetryService.ActivityCompleted(activityId, file.FilePath);
            return new ExportLogsResponse(true);
        }
        catch (OperationCanceledException exception)
        {
            _telemetryService.ActivityCanceled(activityId, exception.Message);
            throw;
        }
        catch (Exception exception)
        {
            _telemetryService.ActivityError(activityId, exception);
            throw;
        }
    }
}
