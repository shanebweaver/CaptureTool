namespace CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;

public interface IChromaKeyAccessService
{
    bool IsChromaKeyEnabled { get; }

    Task<bool> IsChromaKeyAddOnOwnedAsync(CancellationToken cancellationToken);
}
