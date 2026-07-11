using CaptureTool.Infrastructure.Capture.Windows.DependencyInjection;
using CaptureTool.Mcp.CaptureServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddWindowsCaptureDomains()
    .AddSingleton<IPrimaryMonitorCaptureService, PrimaryMonitorCaptureService>()
    .AddSingleton(TimeProvider.System)
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PrimaryMonitorCaptureTool>();

await builder.Build().RunAsync();
