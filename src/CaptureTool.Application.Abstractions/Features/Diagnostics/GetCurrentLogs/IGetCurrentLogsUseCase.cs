using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Diagnostics.GetCurrentLogs;

public interface IGetCurrentLogsUseCase : IUseCase<GetCurrentLogsRequest, GetCurrentLogsResponse>
{
}