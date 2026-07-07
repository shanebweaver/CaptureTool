using CaptureTool.Domain.FileSystem;

namespace CaptureTool.Application.Abstractions.Capture;

public interface IImageCaptureState
{
    event EventHandler<ImageFile>? NewImageCaptured;
}
