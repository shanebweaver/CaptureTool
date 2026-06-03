using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioCapture.ToggleLocalAudioCapture;

public interface IToggleLocalAudioCaptureUseCase : IUseCase<ToggleLocalAudioCaptureRequest, ToggleLocalAudioCaptureResponse>
{
}