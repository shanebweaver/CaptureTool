using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateAudioCaptureDefaultLocalAudio;

internal sealed class UpdateAudioCaptureDefaultLocalAudioUseCase : IUpdateAudioCaptureDefaultLocalAudioUseCase
{
    private const string ActivityId = "UpdateAudioCaptureDefaultLocalAudio";

    private readonly ISettingsService _settingsService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public UpdateAudioCaptureDefaultLocalAudioUseCase(
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateAudioCaptureDefaultLocalAudioRequest request) => true;

    public Task<UseCaseResponse<UpdateAudioCaptureDefaultLocalAudioResponse>> ExecuteAsync(UpdateAudioCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_AudioCapture_DefaultLocalAudioEnabled, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateAudioCaptureDefaultLocalAudioResponse();
            },
            cancellationToken: cancellationToken);
    }
}
