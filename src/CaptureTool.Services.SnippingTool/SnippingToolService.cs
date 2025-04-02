using System;
using System.Threading.Tasks;
using Windows.System;

namespace CaptureTool.Services.SnippingTool;

public class SnippingToolService : ISnippingToolService
{
    public event EventHandler<SnippingToolResponse>? ResponseReceived;

    public void HandleSnippingToolResponse(SnippingToolResponse response)
    {
        ResponseReceived?.Invoke(this, response);
    }

    public async Task LaunchSnippingToolRequestAsync()
    {
        // TODO:  Create SnippingToolRequest object
        string scheme = "ms-screenclip";
        string host = "capture";
        string path = "image";
        string query = "rectangle&redirect-uri=capture-tool://response";
        Uri launchUri = new($"{scheme}://{host}/{path}?{query}");
        await Launcher.LaunchUriAsync(launchUri);
    }
}
