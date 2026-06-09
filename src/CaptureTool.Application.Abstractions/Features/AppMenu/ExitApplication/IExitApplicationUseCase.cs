using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.AppMenu.ExitApplication;

public interface IExitApplicationUseCase : IUseCase<ExitApplicationRequest, ExitApplicationResponse>, IConditional<ExitApplicationRequest>
{
}