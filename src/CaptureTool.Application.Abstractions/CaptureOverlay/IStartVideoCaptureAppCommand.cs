using CaptureTool.Domain.Capture.Abstractions;
using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Application.Abstractions.CaptureOverlay;

public interface IStartVideoCaptureAppCommand : IConditionalAppCommand<NewCaptureArgs> { }
