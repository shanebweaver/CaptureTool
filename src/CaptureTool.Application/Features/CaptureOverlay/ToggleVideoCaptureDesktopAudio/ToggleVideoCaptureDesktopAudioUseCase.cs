using CaptureTool.Application.Abstractions.Capture;
using CaptureTool.Application.Abstractions.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.CaptureOverlay.ToggleVideoCaptureDesktopAudio;

public sealed class ToggleVideoCaptureDesktopAudioUseCase : IToggleVideoCaptureDesktopAudioUseCase
{
    private const string ActivityId = "ToggleVideoCaptureDesktopAudio";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureHandler _videoCaptureHandler;

    public ToggleVideoCaptureDesktopAudioUseCase(IVideoCaptureHandler videoCaptureHandler,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _videoCaptureHandler = videoCaptureHandler;
    }

    public Task<UseCaseResponse<ToggleVideoCaptureDesktopAudioResponse>> ExecuteAsync(ToggleVideoCaptureDesktopAudioRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                bool newValue = !_videoCaptureHandler.IsDesktopAudioEnabled;
                _videoCaptureHandler.SetIsDesktopAudioEnabled(newValue);
                _videoCaptureHandler.ToggleDesktopAudioCapture(newValue);
                return new ToggleVideoCaptureDesktopAudioResponse();
            },
            cancellationToken: cancellationToken);
    }
}
