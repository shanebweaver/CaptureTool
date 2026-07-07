using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.OpenAudioFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.OpenAudioFolder;

internal sealed class OpenAudioFolderUseCase : IOpenAudioFolderUseCase
{
    private const string ActivityId = "OpenAudioFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storageService;
    private readonly IFolderLauncher _folderLauncher;

    public OpenAudioFolderUseCase(
        ISettingsService settingsService,
        IStorageService storageService,
        IFolderLauncher folderLauncher,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
        _storageService = storageService;
        _folderLauncher = folderLauncher;
    }

    public bool CanExecute(OpenAudioFolderRequest request) => true;

    public Task<UseCaseResponse<OpenAudioFolderResponse>> ExecuteAsync(OpenAudioFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                string path = _settingsService.Get(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder);
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = _storageService.GetSystemDefaultMusicFolderPath();
                }

                if (!_folderLauncher.TryOpenFolder(path))
                {
                    return new OpenAudioFolderResponse(false);
                }

                return new OpenAudioFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
