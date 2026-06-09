using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Store.GetChromaKeyAddOn;

public interface IGetChromaKeyAddOnUseCase : IUseCase<GetChromaKeyAddOnRequest, GetChromaKeyAddOnResponse>, IConditional<GetChromaKeyAddOnRequest>
{
}