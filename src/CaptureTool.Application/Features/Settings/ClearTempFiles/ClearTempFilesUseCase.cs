using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Logging;
using CaptureTool.Infrastructure.Abstractions.Storage;

namespace CaptureTool.Application.Features.Settings.ClearTempFiles;

public sealed class ClearTempFilesUseCase : IUseCase<ClearTempFilesRequest, ClearTempFilesResponse>, IConditional<ClearTempFilesRequest>
{
    private readonly ILogService _logService;
    private readonly IStorageService _storageService;

    public ClearTempFilesUseCase(ILogService logService, IStorageService storageService)
    {
        _logService = logService;
        _storageService = storageService;
    }

    public Task<bool> CanExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ClearTempFilesResponse> ExecuteAsync(ClearTempFilesRequest request, CancellationToken cancellationToken = default)
    {
        string tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
        foreach (var entry in Directory.EnumerateFileSystemEntries(tempFolderPath))
        {
            try
            {
                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, true);
                }
                else
                {
                    File.Delete(entry);
                }
            }
            catch (Exception ex)
            {
                _logService.LogException(ex, $"Failed to delete temporary file or folder: {entry}");
            }
        }

        return Task.FromResult(new ClearTempFilesResponse());
    }
}