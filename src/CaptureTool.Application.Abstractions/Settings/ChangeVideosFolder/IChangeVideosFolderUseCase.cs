using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.ChangeVideosFolder;

public interface IChangeVideosFolderUseCase : IUseCase<ChangeVideosFolderRequest, ChangeVideosFolderResponse>, IConditional<ChangeVideosFolderRequest>
{
}