using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioCapture.PauseAudioCapture;

public interface IPauseAudioCaptureUseCase : IUseCase<PauseAudioCaptureRequest, PauseAudioCaptureResponse>
{
}