using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.OpenScreenshotsFolder;

public interface IOpenScreenshotsFolderUseCase : IUseCase<OpenScreenshotsFolderRequest, OpenScreenshotsFolderResponse>, IConditional<OpenScreenshotsFolderRequest>
{
}