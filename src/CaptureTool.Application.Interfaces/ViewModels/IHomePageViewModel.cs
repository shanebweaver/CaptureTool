using CaptureTool.Common.Commands;

namespace CaptureTool.Application.Interfaces.ViewModels;

public interface IHomePageViewModel
{
    RelayCommand NewImageCaptureCommand { get; }
    RelayCommand NewVideoCaptureCommand { get; }
    bool IsVideoCaptureEnabled { get; }
}
