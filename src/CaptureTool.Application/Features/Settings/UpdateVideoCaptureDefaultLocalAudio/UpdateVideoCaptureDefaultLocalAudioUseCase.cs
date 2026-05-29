using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Settings;

namespace CaptureTool.Application.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;

public sealed class UpdateVideoCaptureDefaultLocalAudioUseCase : IUseCase<UpdateVideoCaptureDefaultLocalAudioRequest, UpdateVideoCaptureDefaultLocalAudioResponse>, IConditional<UpdateVideoCaptureDefaultLocalAudioRequest>
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureDefaultLocalAudioUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureDefaultLocalAudioRequest request) => true;

    public async Task<UpdateVideoCaptureDefaultLocalAudioResponse> ExecuteAsync(UpdateVideoCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default)
    {
        _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, request.IsEnabled);
        await _settingsService.TrySaveAsync(cancellationToken);
        return new UpdateVideoCaptureDefaultLocalAudioResponse();
    }
}