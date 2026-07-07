using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Diagnostics.ClearLogs;

public interface IClearLogsUseCase : IUseCase<ClearLogsRequest, ClearLogsResponse>
{
}