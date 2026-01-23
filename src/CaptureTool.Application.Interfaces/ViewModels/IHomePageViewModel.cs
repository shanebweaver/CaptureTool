using CaptureTool.Common;
using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IHomePageViewModel : IViewModel
{
    IAppCommand NewImageCaptureCommand { get; }
    IAppCommand NewVideoCaptureCommand { get; }
    bool IsVideoCaptureEnabled { get; }
}
