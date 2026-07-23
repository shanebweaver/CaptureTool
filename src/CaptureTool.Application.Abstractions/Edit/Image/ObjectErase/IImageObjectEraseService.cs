namespace CaptureTool.Application.Abstractions.Edit.Image.ObjectErase;

public interface IImageObjectEraseService
{
    ObjectEraseReadyState GetReadyState();

    Task<ObjectErasePreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<ObjectEraseResult> EraseAsync(
        ObjectEraseRequest request,
        CancellationToken cancellationToken = default);
}
