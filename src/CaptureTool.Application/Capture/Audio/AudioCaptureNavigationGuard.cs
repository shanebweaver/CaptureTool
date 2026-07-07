using CaptureTool.Application.Abstractions.Capture.Audio;

namespace CaptureTool.Application.Capture.Audio;

internal sealed class AudioCaptureNavigationGuard : IAudioCaptureNavigationGuard
{
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;
    private readonly IAudioCaptureNavigationConfirmationService _confirmationService;

    public AudioCaptureNavigationGuard(
        IAudioCaptureWorkflow audioCaptureWorkflow,
        IAudioCaptureNavigationConfirmationService confirmationService)
    {
        _audioCaptureWorkflow = audioCaptureWorkflow;
        _confirmationService = confirmationService;
    }

    public async Task<bool> CanNavigateAwayFromActiveCaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!_audioCaptureWorkflow.IsRecording)
        {
            return true;
        }

        bool shouldStopRecording = await _confirmationService.ConfirmStopActiveRecordingAsync(cancellationToken);
        if (!shouldStopRecording || cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        _audioCaptureWorkflow.StopCapture();
        return true;
    }
}
