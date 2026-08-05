using CaptureTool.Domain.Capture;

namespace CaptureTool.Application.Abstractions.Capture;

public sealed record AudioCaptureStateChange(
    AudioCaptureState State,
    AudioCaptureFailure? Failure = null);
