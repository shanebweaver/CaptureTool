using CaptureTool.Application.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.Features.Store.GetChromaKeyAddOn;

public sealed class GetChromaKeyAddOnUseCase : IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse>, IConditional<GetChromaKeyAddOnRequest>
{
    private readonly IStoreService _storeService;

    public GetChromaKeyAddOnUseCase(IStoreService storeService)
    {
        _storeService = storeService;
    }

    public Task<bool> CanExecuteAsync(GetChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    public async Task<GetChromaKeyAddOnResponse> ExecuteAsync(GetChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        IStoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
        return new GetChromaKeyAddOnResponse(addOn);
    }
}