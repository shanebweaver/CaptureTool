using CaptureTool.Infrastructure.Abstractions.Queries;
using CaptureTool.Infrastructure.Abstractions.Store;

namespace CaptureTool.Application.Abstractions.Store;

public interface IGetChromaKeyAddOnQuery: IAsyncAppQuery<IStoreAddOn>
{
}