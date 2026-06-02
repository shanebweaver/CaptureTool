using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Windowing.ShowMainWindow;

public interface IShowMainWindowUseCase : IUseCase<ShowMainWindowRequest, ShowMainWindowResponse>, IConditional<ShowMainWindowRequest>
{
}