namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public sealed record CaptureRecorderEvent
{
    public CaptureRecorderEventType EventType { get; init; }

    public CaptureV2ResultCode ResultCode { get; init; }

    public ulong Sequence { get; init; }

    public ulong Timestamp100ns { get; init; }

    public uint SourceId { get; init; }

    internal static CaptureRecorderEvent FromNative(CaptureV2NativeEvent eventData)
        => new()
        {
            EventType = (CaptureRecorderEventType)eventData.EventType,
            ResultCode = (CaptureV2ResultCode)eventData.ResultCode,
            Sequence = eventData.Sequence,
            Timestamp100ns = eventData.Timestamp100ns,
            SourceId = eventData.SourceId,
        };

    internal CaptureV2NativeEvent ToNative()
        => new()
        {
            Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<CaptureV2NativeEvent>(),
            Version = CaptureV2NativeMapping.DtoVersion,
            EventType = (int)EventType,
            ResultCode = (int)ResultCode,
            Sequence = Sequence,
            Timestamp100ns = Timestamp100ns,
            SourceId = SourceId,
        };
}

public enum CaptureRecorderEventType
{
    Unknown = 0,
    StateChanged = 1,
    Error = 2,
    Diagnostics = 3,
}

public static class CaptureRecorderEventMasks
{
    public const ulong All = ulong.MaxValue;

    public static ulong For(CaptureRecorderEventType eventType)
        => eventType <= CaptureRecorderEventType.Unknown || (int)eventType >= 64
            ? 0
            : 1UL << (int)eventType;
}
