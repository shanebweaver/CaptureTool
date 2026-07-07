using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.OpenScreenshotsFolder;

public interface IOpenScreenshotsFolderUseCase : IUseCase<OpenScreenshotsFolderRequest, OpenScreenshotsFolderResponse>, IConditional<OpenScreenshotsFolderRequest>
{
}