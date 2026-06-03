using CaptureTool.Application.Abstractions.UseCases;

namespace CaptureTool.Application.Abstractions.Features.Settings.ChangeScreenshotsFolder;

public interface IChangeScreenshotsFolderUseCase : IUseCase<ChangeScreenshotsFolderRequest, ChangeScreenshotsFolderResponse>, IConditional<ChangeScreenshotsFolderRequest>
{
}