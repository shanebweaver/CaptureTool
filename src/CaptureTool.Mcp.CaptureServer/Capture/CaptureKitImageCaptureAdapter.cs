using CaptureTool.Mcp.CaptureServer.Models;
using CaptureTool.Mcp.CaptureServer.Abstractions;
using CaptureKit.Abstractions;
using System.Drawing;

namespace CaptureTool.Mcp.CaptureServer.Capture;

public sealed class CaptureKitImageCaptureAdapter : ICaptureKitImageCaptureAdapter
{
    private readonly IImageCaptureService _imageCaptureService;
    private readonly IMcpCaptureStore _captureStore;
    private readonly TimeProvider _timeProvider;

    public CaptureKitImageCaptureAdapter(
        IImageCaptureService imageCaptureService,
        IMcpCaptureStore captureStore,
        TimeProvider timeProvider)
    {
        _imageCaptureService = imageCaptureService;
        _captureStore = captureStore;
        _timeProvider = timeProvider;
    }

    public McpCapture Capture(
        CaptureTarget target,
        Rectangle sourceBounds,
        string sourceKind,
        uint dpi,
        float scale,
        string? targetId = null,
        string? targetTitle = null,
        Rectangle? monitorBounds = null,
        Rectangle? workAreaBounds = null,
        bool? isPrimary = null)
    {
        string outputPath = Path.Combine(
            Path.GetTempPath(),
            "CaptureTool",
            "McpCapture",
            $"{Guid.NewGuid():N}.png");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            ImageCaptureResult result = _imageCaptureService.Capture(new ImageCaptureRequest(target, outputPath));
            byte[] pngBytes = File.ReadAllBytes(result.FilePath);

            var metadata = McpCaptureMetadata.Create(
                McpCaptureIds.Create(),
                _timeProvider.GetUtcNow(),
                result.Width,
                result.Height,
                dpi,
                scale,
                sourceBounds,
                sourceKind,
                "png",
                monitorBounds,
                workAreaBounds,
                isPrimary,
                targetId,
                targetTitle);

            var capture = new McpCapture(pngBytes, metadata);
            _captureStore.Store(capture);
            return capture;
        }
        finally
        {
            TryDeleteFile(outputPath);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
