using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Diagnostics.ClearLogs;

public interface IClearLogsUseCase : IUseCase<ClearLogsRequest, ClearLogsResponse>
{
}