namespace CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;

public interface IChromaKeyAccessService
{
    bool IsChromaKeyEnabled { get; }

    Task<bool> IsChromaKeyAddOnOwnedAsync(CancellationToken cancellationToken);
}
