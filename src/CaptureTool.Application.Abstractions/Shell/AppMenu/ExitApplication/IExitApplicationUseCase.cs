using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Shell.AppMenu.ExitApplication;

public interface IExitApplicationUseCase : IUseCase<ExitApplicationRequest, ExitApplicationResponse>, IConditional<ExitApplicationRequest>
{
}