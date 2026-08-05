using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateAudioCaptureAutoSave;

internal sealed class UpdateAudioCaptureAutoSaveUseCase : IUpdateAudioCaptureAutoSaveUseCase
{
    private const string ActivityId = "UpdateAudioCaptureAutoSave";

    private readonly ISettingsService _settingsService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public UpdateAudioCaptureAutoSaveUseCase(
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateAudioCaptureAutoSaveRequest request) => true;

    public Task<UseCaseResponse<UpdateAudioCaptureAutoSaveResponse>> ExecuteAsync(UpdateAudioCaptureAutoSaveRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_AudioCapture_AutoSave,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateAudioCaptureAutoSaveResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
