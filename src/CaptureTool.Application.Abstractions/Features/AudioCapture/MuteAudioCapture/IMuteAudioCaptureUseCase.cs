using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AudioCapture.MuteAudioCapture;

public interface IMuteAudioCaptureUseCase : IUseCase<MuteAudioCaptureRequest, MuteAudioCaptureResponse>
{
}