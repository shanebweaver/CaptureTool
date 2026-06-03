using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.OpenVideosFolder;

public interface IOpenVideosFolderUseCase : IUseCase<OpenVideosFolderRequest, OpenVideosFolderResponse>, IConditional<OpenVideosFolderRequest>
{
}