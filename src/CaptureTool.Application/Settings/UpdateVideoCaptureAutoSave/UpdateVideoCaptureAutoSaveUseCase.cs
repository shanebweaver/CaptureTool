using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoSave;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateVideoCaptureAutoSave;

internal sealed class UpdateVideoCaptureAutoSaveUseCase : IUpdateVideoCaptureAutoSaveUseCase
{
    private const string ActivityId = "UpdateVideoCaptureAutoSave";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoSaveUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoSaveRequest request) => true;

    public Task<UseCaseResponse<UpdateVideoCaptureAutoSaveResponse>> ExecuteAsync(UpdateVideoCaptureAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_VideoCapture_AutoSave,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateVideoCaptureAutoSaveResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
