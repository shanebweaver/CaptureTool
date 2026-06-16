using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Store.PurchaseChromaKeyAddOn;

public sealed class PurchaseChromaKeyAddOnUseCase : IPurchaseChromaKeyAddOnUseCase
{
    private const string ActivityId = "PurchaseChromaKeyAddOn";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStoreService _storeService;

    public PurchaseChromaKeyAddOnUseCase(IStoreService storeService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _storeService = storeService;
    }

    public bool CanExecute(PurchaseChromaKeyAddOnRequest request)
    {
        return true;
    }

    public Task<UseCaseResponse<PurchaseChromaKeyAddOnResponse>> ExecuteAsync(PurchaseChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                bool success = await _storeService.PurchaseAddonAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
                return new PurchaseChromaKeyAddOnResponse(success);
            },
            cancellationToken: cancellationToken);
    }
}
