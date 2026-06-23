using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.AudioCapture;

namespace CaptureTool.Application.Features.AudioCapture;

public sealed class AudioCaptureNavigationGuard : IAudioCaptureNavigationGuard
{
    private readonly IAudioCaptureHandler _audioCaptureHandler;
    private readonly IAudioCaptureNavigationConfirmationService _confirmationService;

    public AudioCaptureNavigationGuard(
        IAudioCaptureHandler audioCaptureHandler,
        IAudioCaptureNavigationConfirmationService confirmationService)
    {
        _audioCaptureHandler = audioCaptureHandler;
        _confirmationService = confirmationService;
    }

    public async Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!_audioCaptureHandler.IsRecording)
        {
            return true;
        }

        bool shouldStopRecording = await _confirmationService.ConfirmStopActiveRecordingAsync(cancellationToken);
        if (!shouldStopRecording || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        _audioCaptureHandler.StopCapture();
        return true;
    }
}
