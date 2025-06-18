using System;
using System.Threading.Tasks;

namespace CaptureTool.Capture.Windows.SnippingTool;

public interface ISnippingToolService
{
    event EventHandler<SnippingToolResponse>? ResponseReceived;

    bool IsSnippingToolInstalled();

    void HandleSnippingToolResponse(SnippingToolResponse response);

    Task DiscoverSupportAsync();
    Task CaptureImageAsync(SnippingToolCaptureOptions options);
    Task CaptureVideoAsync(SnippingToolCaptureOptions options);
}