using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Diagnostics.GetCurrentLogs;

public interface IGetCurrentLogsUseCase : IUseCase<GetCurrentLogsRequest, GetCurrentLogsResponse>
{
}