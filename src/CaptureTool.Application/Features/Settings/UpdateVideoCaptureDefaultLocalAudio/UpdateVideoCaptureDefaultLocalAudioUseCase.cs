using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;

public sealed class UpdateVideoCaptureDefaultLocalAudioUseCase : IUseCase<UpdateVideoCaptureDefaultLocalAudioRequest, UpdateVideoCaptureDefaultLocalAudioResponse>, IConditional<UpdateVideoCaptureDefaultLocalAudioRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureDefaultLocalAudioUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public Task<bool> CanExecuteAsync(UpdateVideoCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default) => Task.FromResult(true);

    public async Task<UpdateVideoCaptureDefaultLocalAudioResponse> ExecuteAsync(UpdateVideoCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoCaptureDefaultLocalAudioResponse();
    }
}