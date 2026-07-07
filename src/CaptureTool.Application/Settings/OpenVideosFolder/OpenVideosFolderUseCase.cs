using CaptureTool.Application.Abstractions.Settings.OpenVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenVideosFolder;

internal sealed class OpenVideosFolderUseCase : IOpenVideosFolderUseCase
{
    private const string ActivityId = "OpenVideosFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IFolderLauncher _folderLauncher;

    public OpenVideosFolderUseCase(ISettingsService settingsService, IStorageService storageService,
        IFolderLauncher folderLauncher,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _storageService = storageService;
        _folderLauncher = folderLauncher;
    }

    public bool CanExecute(OpenVideosFolderRequest request) => true;

    public Task<UseCaseResponse<OpenVideosFolderResponse>> ExecuteAsync(OpenVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var path = _settingsService.Get(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _storageService.GetSystemDefaultVideosFolderPath();
                }

                if (!_folderLauncher.TryOpenFolder(path))
                {
                    return new OpenVideosFolderResponse(false);
                }

                return new OpenVideosFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
