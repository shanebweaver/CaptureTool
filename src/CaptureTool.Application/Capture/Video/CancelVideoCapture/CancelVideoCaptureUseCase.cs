using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Capture.Video.CancelVideoCapture;
using CaptureTool.Application.Abstractions.Settings;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.CancelVideoCapture;

internal sealed class CancelVideoCaptureUseCase : ICancelVideoCaptureUseCase
{
    private const string ActivityId = "CancelVideoCapture";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;
    private readonly ICaptureDiscardConfirmationService _confirmationService;
    private readonly ISettingsService _settingsService;

    public CancelVideoCaptureUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        ICaptureDiscardConfirmationService confirmationService,
        ISettingsService settingsService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _confirmationService = confirmationService;
        _settingsService = settingsService;
    }

    public Task<UseCaseResponse<CancelVideoCaptureResponse>> ExecuteAsync(CancelVideoCaptureRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async token =>
            {
                if (!_videoCaptureWorkflow.IsRecording)
                {
                    return new CancelVideoCaptureResponse();
                }

                bool shouldWarnBeforeDiscard = _settingsService.Get(CaptureToolSettings.Settings_Capture_WarnBeforeDiscard);
                if (shouldWarnBeforeDiscard)
                {
                    bool shouldDiscardCapture = await _confirmationService.ConfirmDiscardActiveCaptureAsync(token);
                    if (!shouldDiscardCapture || token.IsCancellationRequested)
                    {
                        return new CancelVideoCaptureResponse(false);
                    }
                }

                _videoCaptureWorkflow.CancelVideoCapture();
                return new CancelVideoCaptureResponse();
            },
            cancellationToken: cancellationToken);
    }
}
