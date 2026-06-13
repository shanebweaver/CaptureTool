using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Diagnostics.ExportLogs;

public interface IExportLogsUseCase : IUseCase<ExportLogsRequest, ExportLogsResponse>
{
}
