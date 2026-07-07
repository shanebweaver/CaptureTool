using CaptureTool.Application.Abstractions.Edit.Image.ChromaKey;
using CaptureTool.Application.Abstractions.Store;

namespace CaptureTool.Application.Edit.Image.ChromaKey;

internal sealed class ChromaKeyAccessService : IChromaKeyAccessService
{
    private readonly IChromaKeyFeatureAvailability _featureAvailability;
    private readonly IStoreService _storeService;

    public ChromaKeyAccessService(
        IChromaKeyFeatureAvailability featureAvailability,
        IStoreService storeService)
    {
        _featureAvailability = featureAvailability;
        _storeService = storeService;
    }

    public bool IsChromaKeyEnabled => _featureAvailability.IsChromaKeyEnabled;

    public Task<bool> IsChromaKeyAddOnOwnedAsync(CancellationToken cancellationToken) =>
        !IsChromaKeyEnabled
            ? Task.FromResult(false)
            : _storeService.IsAddonPurchasedAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
}
