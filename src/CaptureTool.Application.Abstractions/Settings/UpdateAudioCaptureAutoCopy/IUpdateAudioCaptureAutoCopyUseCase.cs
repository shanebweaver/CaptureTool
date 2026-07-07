using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoCopy;

public interface IUpdateAudioCaptureAutoCopyUseCase : IUseCase<UpdateAudioCaptureAutoCopyRequest, UpdateAudioCaptureAutoCopyResponse>, IConditional<UpdateAudioCaptureAutoCopyRequest>
{
}
