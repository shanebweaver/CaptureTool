using CaptureTool.Application.Abstractions.Capture.Video.SetVideoCaptureDesktopAudioVolume;
using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Application.UseCases;

namespace CaptureTool.Application.Capture.Video.SetVideoCaptureDesktopAudioVolume;

internal sealed class SetVideoCaptureDesktopAudioVolumeUseCase : ISetVideoCaptureDesktopAudioVolumeUseCase
{
    private const string ActivityId = "SetVideoCaptureDesktopAudioVolume";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IVideoCaptureWorkflow _videoCaptureWorkflow;

    public SetVideoCaptureDesktopAudioVolumeUseCase(
        IVideoCaptureWorkflow videoCaptureWorkflow,
        IUseCaseExecutor useCaseExecutor)
    {
        _videoCaptureWorkflow = videoCaptureWorkflow;
        _useCaseExecutor = useCaseExecutor;
    }

    public Task<UseCaseResponse<SetVideoCaptureDesktopAudioVolumeResponse>> ExecuteAsync(
        SetVideoCaptureDesktopAudioVolumeRequest request,
        CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: () =>
            {
                _videoCaptureWorkflow.SetDesktopAudioVolume(request.VolumePercentage);
                return new SetVideoCaptureDesktopAudioVolumeResponse();
            },
            cancellationToken: cancellationToken);
    }
}
