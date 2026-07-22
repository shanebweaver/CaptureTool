using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Audio.CancelAudioCapture;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Audio.CancelAudioCapture;

internal sealed class CancelAudioCaptureUseCase : ICancelAudioCaptureUseCase
{
    private const string ActivityId = "CancelAudioCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IAudioCaptureWorkflow _audioCaptureWorkflow;
    private readonly ICaptureDiscardConfirmationService _confirmationService;
    private readonly ISettingsService _settingsService;

    public CancelAudioCaptureUseCase(
        IAudioCaptureWorkflow audioCaptureWorkflow,
        ICaptureDiscardConfirmationService confirmationService,
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _audioCaptureWorkflow = audioCaptureWorkflow;
        _confirmationService = confirmationService;
        _settingsService = settingsService;
    }

    public Task<UseCaseResponse<CancelAudioCaptureResponse>> ExecuteAsync(
        CancelAudioCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                if (!_audioCaptureWorkflow.IsRecording)
                {
                    return new CancelAudioCaptureResponse(false);
                }

                bool shouldWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard);
                if (shouldWarnBeforeDiscard)
                {
                    bool shouldDiscardRecording = await _confirmationService.ConfirmDiscardActiveCaptureAsync(token);
                    if (!shouldDiscardRecording || token.IsCancellationRequested)
                    {
                        return new CancelAudioCaptureResponse(false);
                    }
                }

                _audioCaptureWorkflow.CancelCapture();
                return new CancelAudioCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
