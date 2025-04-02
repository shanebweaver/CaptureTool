using System;
using System.Threading.Tasks;

namespace CaptureTool.Services.SnippingTool;

public interface ISnippingToolService
{
    event EventHandler<SnippingToolResponse>? ResponseReceived;

    void HandleSnippingToolResponse(SnippingToolResponse response);
    Task LaunchSnippingToolRequestAsync();
}