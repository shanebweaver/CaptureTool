using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateVideoCaptureDefaultLocalAudio;

internal sealed class UpdateVideoCaptureDefaultLocalAudioUseCase : IUpdateVideoCaptureDefaultLocalAudioUseCase
{
    private const string ActivityId = "UpdateVideoCaptureDefaultLocalAudio";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureDefaultLocalAudioUseCase(ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureDefaultLocalAudioRequest request) => true;

    public Task<UseCaseResponse<UpdateVideoCaptureDefaultLocalAudioResponse>> ExecuteAsync(UpdateVideoCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                SettingsMutationResult result = await _settingsService.TrySetAndSaveAsync(
                    CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled,
                    request.IsEnabled,
                    cancellationToken);
                return new UpdateVideoCaptureDefaultLocalAudioResponse(result.Succeeded);
            },
            cancellationToken: cancellationToken);
    }
}
