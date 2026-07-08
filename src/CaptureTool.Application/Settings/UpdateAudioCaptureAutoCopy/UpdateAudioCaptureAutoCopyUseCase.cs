using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Settings.UpdateAudioCaptureAutoCopy;

internal sealed class UpdateAudioCaptureAutoCopyUseCase : IUpdateAudioCaptureAutoCopyUseCase
{
    private const string ActivityId = "UpdateAudioCaptureAutoCopy";

    private readonly ISettingsService _settingsService;
    private readonly IUseCaseExecutor _useCaseExecutor;

    public UpdateAudioCaptureAutoCopyUseCase(
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _settingsService = settingsService;
        _useCaseExecutor = useCaseExecutor;
    }

    public bool CanExecute(UpdateAudioCaptureAutoCopyRequest request) => true;

    public Task<UseCaseResponse<UpdateAudioCaptureAutoCopyResponse>> ExecuteAsync(UpdateAudioCaptureAutoCopyRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                _settingsService.Set(CaptureToolSettings.Settings_AudioCapture_AutoCopy, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateAudioCaptureAutoCopyResponse();
            },
            cancellationToken: cancellationToken);
    }
}
