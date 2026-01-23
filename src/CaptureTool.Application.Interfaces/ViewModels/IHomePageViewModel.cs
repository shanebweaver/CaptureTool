using System.Windows.Input;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IHomePageViewModel
{
    ICommand NewImageCaptureCommand { get; }
    ICommand NewVideoCaptureCommand { get; }
    bool IsVideoCaptureEnabled { get; }
}
