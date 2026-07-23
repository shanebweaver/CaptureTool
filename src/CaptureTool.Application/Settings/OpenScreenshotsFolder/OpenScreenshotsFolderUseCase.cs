using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenScreenshotsFolder;

internal sealed class OpenScreenshotsFolderUseCase : IOpenScreenshotsFolderUseCase
{
    private const string ActivityId = "OpenScreenshotsFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IFolderLauncher _folderLauncher;

    public OpenScreenshotsFolderUseCase(ISettingsService settingsService, IStorageService storageService,
        IFolderLauncher folderLauncher,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _storageService = storageService;
        _folderLauncher = folderLauncher;
    }

    public bool CanExecute(OpenScreenshotsFolderRequest request) => true;

    public Task<UseCaseResponse<OpenScreenshotsFolderResponse>> ExecuteAsync(OpenScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                var path = _settingsService.Get(CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _storageService.GetSystemDefaultScreenshotsFolderPath();
                }

                if (!_folderLauncher.TryOpenFolder(path))
                {
                    return new OpenScreenshotsFolderResponse(false);
                }

                return new OpenScreenshotsFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
