using CaptureTool.Application.Abstractions.Features.Store;
using CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;
using CaptureTool.Application.Abstractions.Store;
using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Features.Store.GetChromaKeyAddOn;

public sealed class GetChromaKeyAddOnUseCase : IGetChromaKeyAddOnUseCase
{
    private const string ActivityId = "GetChromaKeyAddOn";

    private readonly IUseCaseExecutor _useCaseExecutor;
    private readonly IStoreService _storeService;

    public GetChromaKeyAddOnUseCase(IStoreService storeService,
        IUseCaseExecutor useCaseExecutor)
    {
        _useCaseExecutor = useCaseExecutor;
        _storeService = storeService;
    }

    public bool CanExecute(GetChromaKeyAddOnRequest request)
    {
        return true;
    }

    public Task<UseCaseResponse<GetChromaKeyAddOnResponse>> ExecuteAsync(GetChromaKeyAddOnRequest request, CancellationToken cancellationToken = default)
    {
        return _useCaseExecutor.ExecuteAsync(
            activityId: ActivityId,
            useCase: async _ =>
            {
                IStoreAddOn? addOn = await _storeService.GetAddonProductInfoAsync(CaptureToolStoreProducts.AddOns.ChromaKeyBackgroundRemoval, cancellationToken);
                return new GetChromaKeyAddOnResponse(addOn);
            },
            cancellationToken: cancellationToken);
    }
}
