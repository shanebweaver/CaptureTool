using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Settings.ChangeScreenshotsFolder;

public interface IChangeScreenshotsFolderUseCase : IUseCase<ChangeScreenshotsFolderRequest, ChangeScreenshotsFolderResponse>, IConditional<ChangeScreenshotsFolderRequest>
{
}