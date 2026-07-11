using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using CaptureTool.Mcp.CaptureServer.DependencyInjection;
using CaptureTool.Mcp.CaptureServer.Platform;
using CaptureTool.Mcp.CaptureServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

WindowsDpiAwareness.EnablePerMonitorV2();

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddWindowsCaptureDomains()
    .AddCaptureServerServices()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PrimaryMonitorCaptureTool>()
    .WithTools<RegionCaptureTool>()
    .WithTools<AnnotationTool>()
    .WithTools<AllScreensCaptureTool>()
    .WithTools<WindowCaptureTool>();

await builder.Build().RunAsync();
