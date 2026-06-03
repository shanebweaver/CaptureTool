using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioCapture.StopAudioCapture;

public interface IStopAudioCaptureUseCase : IUseCase<StopAudioCaptureRequest, StopAudioCaptureResponse>
{
}