using CaptureTool.Infrastructure.Abstractions.Commands;
using CaptureTool.Infrastructure.Abstractions.Queries;

namespace CaptureTool.Application.Abstractions.AppMenu;

public interface IGetRecentCapturesAppQuery : IAppQuery<IEnumerable<IRecentCapture>>
{
}