using CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.ChangeVideosFolder;

public sealed class ChangeVideosFolderUseCase : IChangeVideosFolderUseCase
{
    private const string ActivityId = "ChangeVideosFolder";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IFilePickerService _picker;
    private readonly ISettingsService _settings;

    public ChangeVideosFolderUseCase(IFilePickerService picker, ISettingsService settings,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _picker = picker;
        _settings = settings;
    }

    public bool CanExecute(ChangeVideosFolderRequest request) => true;

    public Task<UseCaseResponse<ChangeVideosFolderResponse>> ExecuteAsync(ChangeVideosFolderRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                var folder = await _picker.PickFolderAsync(UserFolder.Videos);
                if (folder is null)
                {
                    return new ChangeVideosFolderResponse(false);
                }

                _settings.Set(CaptureToolSettings.Settings_VideoCapture_AutoSaveFolder, folder.FolderPath);
                await _settings.TrySaveAsync(cancellationToken);
                return new ChangeVideosFolderResponse();
            },
            cancellationToken: cancellationToken);
    }
}
