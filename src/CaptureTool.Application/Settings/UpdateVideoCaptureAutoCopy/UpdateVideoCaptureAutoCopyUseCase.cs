using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureAutoCopy;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateVideoCaptureAutoCopy;

internal sealed class UpdateVideoCaptureAutoCopyUseCase : IUpdateVideoCaptureAutoCopyUseCase
{
    private const string ActivityId = "UpdateVideoCaptureAutoCopy";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureAutoCopyUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureAutoCopyRequest request) => true;

    public Task<UseCaseResponse<UpdateVideoCaptureAutoCopyResponse>> ExecuteAsync(UpdateVideoCaptureAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_VideoCapture_AutoCopy,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateVideoCaptureAutoCopyResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
