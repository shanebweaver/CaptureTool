using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.ChangeVideosFolder;

public interface IChangeVideosFolderUseCase : IUseCase<ChangeVideosFolderRequest, ChangeVideosFolderResponse>, IConditional<ChangeVideosFolderRequest>
{
}