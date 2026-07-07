using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.OpenTempFolder;

public interface IOpenTempFolderUseCase : IUseCase<OpenTempFolderRequest, OpenTempFolderResponse>, IConditional<OpenTempFolderRequest>
{
}