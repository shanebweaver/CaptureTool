using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Store.PurchaseChromaKeyAddOn;

public interface IPurchaseChromaKeyAddOnUseCase : IUseCase<PurchaseChromaKeyAddOnRequest, PurchaseChromaKeyAddOnResponse>, IConditional<PurchaseChromaKeyAddOnRequest>
{
}