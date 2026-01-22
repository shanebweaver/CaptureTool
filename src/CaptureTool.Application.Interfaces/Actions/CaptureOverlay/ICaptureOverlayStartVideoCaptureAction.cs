using CaptureTool.Common.Commands;
using CaptureTool.Domain.Capture.Interfaces;

namespace CaptureTool.Application.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayStartVideoCaptureAction : IActionCommand<NewCaptureArgs> { }
