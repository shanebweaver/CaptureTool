using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.ClearTempFiles;

public interface IClearTempFilesUseCase : IUseCase<ClearTempFilesRequest, ClearTempFilesResponse>, IConditional<ClearTempFilesRequest>
{
}