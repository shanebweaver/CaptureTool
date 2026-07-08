using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.ChangeAudioFolder;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.ChangeAudioFolder;

internal sealed class ChangeAudioFolderUseCase : IChangeAudioFolderUseCase
{
    private const string ActivityId = "ChangeAudioFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeAudioFolderUseCase(
        IFilePickerService picker,
        ISettingsService settings,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeAudioFolderRequest request) => true;

    public Task<UseCaseResponse<ChangeAudioFolderResponse>> ExecuteAsync(ChangeAudioFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                IFolder? folder = await _picker.PickFolderAsync(UserFolder.Music);
                if (folder is null)
                {
                    return new ChangeAudioFolderResponse(false);
                }

                _settings.Set(CaptureToolSettings.Settings_AudioCapture_AutoSaveFolder, folder.FolderPath);
                await _settings.TrySaveAsync(cancellationToken);
                return new ChangeAudioFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
