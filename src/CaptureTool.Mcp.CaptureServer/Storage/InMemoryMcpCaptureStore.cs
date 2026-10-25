using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;

namespace CaptureTool.Mcp.CaptureServer.Storage;

public sealed class InMemoryMcpCaptureStore : IMcpCaptureStore
{
    public const int DefaultMaxCaptures = 20;
    public const long DefaultMaxCaptureBytes = 32 * 1024 * 1024;
    public const long DefaultMaxRetainedBytes = 128 * 1024 * 1024;

    private readonly int _maxCaptures;
    private readonly long _maxCaptureBytes;
    private readonly long _maxRetainedBytes;
    private readonly object _gate = new();
    private readonly Dictionary<string, (McpCapture Capture, LinkedListNode<string> Node)> _captures = [];
    private readonly LinkedList<string> _captureOrder = new();
    private long _retainedBytes;

    public InMemoryMcpCaptureStore(
        int maxCaptures = DefaultMaxCaptures,
        long maxCaptureBytes = DefaultMaxCaptureBytes,
        long maxRetainedBytes = DefaultMaxRetainedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCaptures);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCaptureBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxRetainedBytes);
        _maxCaptures = maxCaptures;
        _maxCaptureBytes = maxCaptureBytes;
        _maxRetainedBytes = maxRetainedBytes;
    }

    public void Store(McpCapture capture)
    {
        long captureBytes = capture.PngBytes.LongLength;
        if (captureBytes > _maxCaptureBytes || captureBytes > _maxRetainedBytes)
        {
            throw new InvalidOperationException(
                $"Capture PNG size ({captureBytes} bytes) exceeds the capture storage limit "
                + $"({Math.Min(_maxCaptureBytes, _maxRetainedBytes)} bytes). Capture a smaller region and retry.");
        }

        lock (_gate)
        {
            // Replacements count only their new bytes and become the newest entry.
            if (_captures.Remove(capture.Metadata.CaptureId, out var previous))
            {
                _retainedBytes -= previous.Capture.PngBytes.LongLength;
                _captureOrder.Remove(previous.Node);
            }

            while (_captures.Count >= _maxCaptures || _retainedBytes > _maxRetainedBytes - captureBytes)
            {
                string evictedCaptureId = _captureOrder.First!.Value;
                _retainedBytes -= _captures[evictedCaptureId].Capture.PngBytes.LongLength;
                _captures.Remove(evictedCaptureId);
                _captureOrder.RemoveFirst();
            }

            LinkedListNode<string> node = _captureOrder.AddLast(capture.Metadata.CaptureId);
            _captures.Add(capture.Metadata.CaptureId, (capture, node));
            _retainedBytes += captureBytes;
        }
    }

    public bool TryGet(string captureId, out McpCapture capture)
    {
        lock (_gate)
        {
            bool found = _captures.TryGetValue(captureId, out var entry);
            capture = entry.Capture!;
            return found;
        }
    }
}
