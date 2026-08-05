using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.ChangeScreenshotsFolder;

internal sealed class ChangeScreenshotsFolderUseCase : IChangeScreenshotsFolderUseCase
{
    private const string ActivityId = "ChangeScreenshotsFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeScreenshotsFolderUseCase(IFilePickerService picker,
        ISettingsService settings,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeScreenshotsFolderRequest request) => true;

    public Task<UseCaseResponse<ChangeScreenshotsFolderResponse>> ExecuteAsync(ChangeScreenshotsFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                var folder = await _picker.PickFolderAsync(UserFolder.Pictures);
                if (folder is null)
                {
                    return new ChangeScreenshotsFolderResponse(false);
                }

                SettingsMutationResult result = await _settings.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_ImageCapture_AutoSaveFolder,
                    folder.FolderPath,
                    cancellationToken);
                return new ChangeScreenshotsFolderResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
