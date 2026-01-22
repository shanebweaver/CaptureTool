using CaptureTool.Common.Commands;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.UseCases.CaptureOverlay;

public interface ICaptureOverlayStartVideoCaptureUseCase : IActionCommand<NewCaptureArgs> { }
