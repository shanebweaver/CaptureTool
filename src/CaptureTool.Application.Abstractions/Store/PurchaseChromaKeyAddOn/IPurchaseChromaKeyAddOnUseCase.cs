using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Store.PurchaseChromaKeyAddOn;

public interface IPurchaseChromaKeyAddOnUseCase : IUseCase<PurchaseChromaKeyAddOnRequest, PurchaseChromaKeyAddOnResponse>, IConditional<PurchaseChromaKeyAddOnRequest>
{
}