using CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.Settings.OpenTempFolder;

internal sealed class OpenTempFolderUseCase : IOpenTempFolderUseCase
{
    private const string ActivityId = "OpenTempFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStorageService _storageService;
    private readonly IFolderLauncher _folderLauncher;

    public OpenTempFolderUseCase(IStorageService storageService,
        IFolderLauncher folderLauncher,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _storageService = storageService;
        _folderLauncher = folderLauncher;
    }

    public bool CanExecute(OpenTempFolderRequest request) => true;

    public Task<UseCaseResponse<OpenTempFolderResponse>> ExecuteAsync(OpenTempFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var tempFolderPath = _storageService.GetApplicationTemporaryFolderPath();
                if (!_folderLauncher.TryOpenFolder(tempFolderPath))
                {
                    return new OpenTempFolderResponse(false);
                }

                return new OpenTempFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
