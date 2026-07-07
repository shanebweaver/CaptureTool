using CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Logging;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;
using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Features.Diagnostics.ExportLogs;

internal sealed class ExportLogsUseCase : IExportLogsUseCase
{
    private const string ActivityId = "ExportLogs";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IFileSystem _fileSystem;

    public ExportLogsUseCase(IFilePickerService filePickerService,
        ILogService logService,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _filePickerService = filePickerService;
        _logService = logService;
        _fileSystem = fileSystem;
    }

    public Task<UseCaseResponse<ExportLogsResponse>> ExecuteAsync(ExportLogsRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                FileReference? file = await _filePickerService.PickSaveFileAsync(FilePickerType.Text, UserFolder.Documents);
                if (file is null || cancellationToken.IsCancellationRequested)
                {
                    return new ExportLogsResponse(false);
                }

                string logs = string.Join(Environment.NewLine, _logService.GetLogs().Select(log => log.ToString()));
                await _fileSystem.WriteAllTextAsync(file.FilePath, logs, cancellationToken);

                return new ExportLogsResponse(true);
            },
            cancellationToken: cancellationToken);
    }
}
