using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.UpdateAudioCaptureAutoSave;

public interface IUpdateAudioCaptureAutoSaveUseCase : IUseCase<UpdateAudioCaptureAutoSaveRequest, UpdateAudioCaptureAutoSaveResponse>, IConditional<UpdateAudioCaptureAutoSaveRequest>
{
}
