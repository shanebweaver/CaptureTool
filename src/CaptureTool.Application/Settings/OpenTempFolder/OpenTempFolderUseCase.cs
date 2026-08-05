using CaptureTool.Application.Abstractions.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Files;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenTempFolder;

internal sealed class OpenTempFolderUseCase : IOpenTempFolderUseCase
{
    private const string ActivityId = "OpenTempFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStorageService _storageService;
    private readonly IFolderLauncher _folderLauncher;
    private readonly IFileSystem _fileSystem;

    public OpenTempFolderUseCase(IStorageService storageService,
        IFolderLauncher folderLauncher,
        IFileSystem fileSystem,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _storageService = storageService;
        _folderLauncher = folderLauncher;
        _fileSystem = fileSystem;
    }

    public bool CanExecute(OpenTempFolderRequest request) => true;

    public Task<UseCaseResponse<OpenTempFolderResponse>> ExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var tempFolderPath = _storageService.GetApplicationScratchFolderPath();
                _fileSystem.CreateDirectory(tempFolderPath);
                if (!_folderLauncher.TryOpenFolder(tempFolderPath))
                {
                    return new OpenTempFolderResponse(false);
                }

                return new OpenTempFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
