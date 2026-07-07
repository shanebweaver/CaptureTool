using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Store.GetChromaKeyAddOn;

public interface IGetChromaKeyAddOnUseCase : IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse>, IConditional<GetChromaKeyAddOnRequest>
{
}