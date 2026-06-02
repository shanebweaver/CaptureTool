using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.OpenTempFolder;

public interface IOpenTempFolderUseCase : IUseCase<OpenTempFolderRequest, OpenTempFolderResponse>, IConditional<OpenTempFolderRequest>
{
}