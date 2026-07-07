using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Windowing.ShowMainWindow;

public interface IShowMainWindowUseCase : IUseCase<ShowMainWindowRequest, ShowMainWindowResponse>, IConditional<ShowMainWindowRequest>
{
}