namespace CaptureTool.Application.Abstractions.Capture;

public readonly record struct AudioCaptureLevel(
    double PeakLevel,
    double RootMeanSquareLevel,
    long Timestamp);
