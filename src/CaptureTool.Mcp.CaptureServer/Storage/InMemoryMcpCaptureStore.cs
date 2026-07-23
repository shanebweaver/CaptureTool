using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureTool.Mcp.CaptureServer.Models;
namespace CaptureTool.Mcp.CaptureServer.Storage;

public sealed class InMemoryMcpCaptureStore : IMcpCaptureStore
{
    private const int MaxCaptures = 20;
    private readonly object _gate = new();
    private readonly Dictionary<string, McpCapture> _captures = [];
    private readonly Queue<string> _captureOrder = new();

    public void Store(McpCapture capture)
    {
        lock (_gate)
        {
            if (_captures.ContainsKey(capture.Metadata.CaptureId))
            {
                _captures[capture.Metadata.CaptureId] = capture;
                return;
            }

            _captures.Add(capture.Metadata.CaptureId, capture);
            _captureOrder.Enqueue(capture.Metadata.CaptureId);

            while (_captures.Count > MaxCaptures)
            {
                string evictedCaptureId = _captureOrder.Dequeue();
                _captures.Remove(evictedCaptureId);
            }
        }
    }

    public bool TryGet(string captureId, out McpCapture capture)
    {
        lock (_gate)
        {
            return _captures.TryGetValue(captureId, out capture!);
        }
    }
}
