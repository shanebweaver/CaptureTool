using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.ClearTempFiles;

public interface IClearTempFilesUseCase : IUseCase<ClearTempFilesRequest, ClearTempFilesResponse>, IConditional<ClearTempFilesRequest>
{
}