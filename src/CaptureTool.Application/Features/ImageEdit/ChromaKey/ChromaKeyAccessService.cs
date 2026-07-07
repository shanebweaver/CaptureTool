using CaptureTool.Application.Abstractions.Features.ImageEdit.ChromaKey;
using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Store;

namespace CaptureTool.Application.Features.ImageEdit.ChromaKey;

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
