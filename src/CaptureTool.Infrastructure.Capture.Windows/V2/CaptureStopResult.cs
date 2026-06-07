namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public sealed record CaptureStopResult
{
    public required CaptureV2ResultCode ResultCode { get; init; }
    public int FinalState { get; init; }
    public int FailureStage { get; init; }
    public ulong DroppedVideoFrames { get; init; }
    public ulong AudioDiscontinuities { get; init; }
    public ulong LateSamples { get; init; }
    public ulong UnsupportedCommands { get; init; }
    public ulong ValidationWarnings { get; init; }

    internal static CaptureStopResult FromNative(CaptureV2NativeStopResult result)
        => new()
        {
            ResultCode = (CaptureV2ResultCode)result.ResultCode,
            FinalState = result.FinalState,
            FailureStage = result.FailureStage,
            DroppedVideoFrames = result.DroppedVideoFrames,
            AudioDiscontinuities = result.AudioDiscontinuities,
            LateSamples = result.LateSamples,
            UnsupportedCommands = result.UnsupportedCommands,
            ValidationWarnings = result.ValidationWarnings,
        };
}
