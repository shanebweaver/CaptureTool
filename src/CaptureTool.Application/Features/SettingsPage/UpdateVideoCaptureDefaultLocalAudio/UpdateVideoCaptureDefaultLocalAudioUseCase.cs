using CaptureTool.Application.Abstractions.Features.Settings.UpdateVideoCaptureDefaultLocalAudio;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Features.Settings;

namespace CaptureTool.Application.Features.SettingsPage.UpdateVideoCaptureDefaultLocalAudio;

public sealed class UpdateVideoCaptureDefaultLocalAudioUseCase : IUpdateVideoCaptureDefaultLocalAudioUseCase
{
    private readonly ISettingsService _settingsService;

    public UpdateVideoCaptureDefaultLocalAudioUseCase(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public bool CanExecute(UpdateVideoCaptureDefaultLocalAudioRequest request) => true;

    public async Task<UpdateVideoCaptureDefaultLocalAudioResponse> ExecuteAsync(UpdateVideoCaptureDefaultLocalAudioRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _settingsService.Set(CaptureToolSettings.Settings_VideoCapture_DefaultLocalAudioEnabled, request.IsEnabled);
            await _settingsService.TrySaveAsync(cancellationToken);
            return new UpdateVideoCaptureDefaultLocalAudioResponse();
        }
        catch (Exception)
        {
            return new UpdateVideoCaptureDefaultLocalAudioResponse(false);
        }
    }
}
