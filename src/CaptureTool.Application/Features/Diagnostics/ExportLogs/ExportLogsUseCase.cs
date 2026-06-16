using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Domain.Capture.Files;

namespace CaptureTool.Application.Features.Diagnostics.ExportLogs;

public sealed class ExportLogsUseCase : IExportLogsUseCase
{
    private const string ActivityId = "ExportLogs";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;

    public ExportLogsUseCase(IFilePickerService filePickerService,
        ILogService logService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _logService = logService;
    }

    public Task<UseCaseResponse<ExportLogsResponse>> ExecuteAsync(ExportLogsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                IFile? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents);
                if (file is null || cancellationToken.IsCancellationRequested)
                {
                    return new ExportLogsResponse(false);
                }

                string logs = string.Join(Environment.NewLine, _logService.GetLogs().Select(log => log.ToString()));
                await File.WriteAllTextAsync(file.FilePath, logs, cancellationToken);

                return new ExportLogsResponse(true);
            },
            cancellationToken: cancellationToken);
    }
}
