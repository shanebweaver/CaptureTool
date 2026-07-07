using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.Features.VideoCapture;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;

internal sealed class ToggleVideoCaptureDesktopAudioUseCase : IToggleVideoCaptureDesktopAudioUseCase
{
    private const string ActivityId = "ToggleVideoCaptureDesktopAudio";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public ToggleVideoCaptureDesktopAudioUseCase(IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureWorkflow = videoCaptureWorkflow;
    }

    public Task<UseCaseResponse<ToggleVideoCaptureDesktopAudioResponse>> ExecuteAsync(ToggleVideoCaptureDesktopAudioRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool newValue = !_videoCaptureWorkflow.IsDesktopAudioEnabled;
                _videoCaptureWorkflow.SetIsDesktopAudioEnabled(newValue);
                _videoCaptureWorkflow.ToggleDesktopAudioCapture(newValue);
                return new ToggleVideoCaptureDesktopAudioResponse();
            },
            cancellationToken: cancellationToken);
    }
}
