using CaptureTool.Common.Commands;
using CaptureTool.Domains.Capture.Interfaces;

namespace CaptureTool.Core.Interfaces.Actions.CaptureOverlay;

public interface ICaptureOverlayStartVideoCaptureAction : IActionCommand<NewCaptureArgs> { }
