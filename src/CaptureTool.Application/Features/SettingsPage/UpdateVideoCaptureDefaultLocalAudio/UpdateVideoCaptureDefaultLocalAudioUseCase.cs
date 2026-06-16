using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureDefaultLocalAudio;

public sealed class UpdateVideoCaptureDefaultLocalAudioUseCase : IUpdateVideoCaptureDefaultLocalAudioUseCase
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
                _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, request.IsEnabled);
                await _settingsService.TrySaveAsync(cancellationToken);
                return new UpdateVideoCaptureDefaultLocalAudioResponse();
            },
            cancellationToken: cancellationToken);
    }
}
