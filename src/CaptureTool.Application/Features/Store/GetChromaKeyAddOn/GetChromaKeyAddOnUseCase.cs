using CaptureTool.Application.Abstractions.UseCases;
using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.Features.Store.GetChromaKeyAddOn;

public sealed class GetChromaKeyAddOnUseCase : IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse>, IConditional<GetChromaKeyAddOnRequest>
{
    private readonly IStoreService _storeService;

    public GetChromaKeyAddOnUseCase(IStoreService storeService)
    {
        _storeService = storeService;
    }

    public bool CanExecute(GetChromaKeyAddOnRequest request)
    {
        return true;
    }

    public async Task<GetChromaKeyAddOnResponse> ExecuteAsync(GetChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        IStoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
        return new GetChromaKeyAddOnResponse(addOn);
    }
}