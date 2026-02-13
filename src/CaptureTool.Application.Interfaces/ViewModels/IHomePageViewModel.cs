using CaptureTool.Infrastructure.Interfaces.Commands;
using CaptureTool.Infrastructure.Interfaces.ViewModels;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IHomePageViewModel : IViewModel
{
    IAppCommand NewImageCaptureCommand { get; }
    IAppCommand NewVideoCaptureCommand { get; }
    IAppCommand NewAudioCaptureCommand { get; }
    bool IsVideoCaptureEnabled { get; }
    bool IsAudioCaptureEnabled { get; }
}
