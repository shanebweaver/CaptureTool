using FluentAssertions;
using ModelContextProtocol.Client;

namespace CaptureTool.Mcp.CaptureServer.Tests;

[TestClass]
public sealed class McpServerSmokeTests
{
    [TestMethod]
    public async Task ServerListsPrimaryMonitorCaptureTool()
    {
        string serverAssemblyPath = Path.Combine(AppContext.BaseDirectory, "CaptureTool.Mcp.CaptureServer.dll");
        serverAssemblyPath.Should().Match(path => File.Exists(path), "the referenced server assembly should be copied to the test output");
        List<string> standardErrorLines = [];
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CaptureTool MCP capture smoke test",
            Command = "dotnet",
            Arguments = [serverAssemblyPath],
            WorkingDirectory = AppContext.BaseDirectory,
            ShutdownTimeout = TimeSpan.FromSeconds(5),
            StandardErrorLines = line => standardErrorLines.Add(line),
        });
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await using var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationTokenSource.Token);

        IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: cancellationTokenSource.Token);

        tools.Should().ContainSingle(tool =>
            tool.Name == "capture_primary_monitor"
            && tool.Title == "Capture Primary Monitor"
            && tool.Description.Contains("primary monitor", StringComparison.OrdinalIgnoreCase));
        tools.Should().ContainSingle(tool =>
            tool.Name == "capture_region"
            && tool.Title == "Capture Region"
            && tool.Description.Contains("region", StringComparison.OrdinalIgnoreCase));
        tools.Should().ContainSingle(tool =>
            tool.Name == "annotate_image"
            && tool.Title == "Annotate Image"
            && tool.Description.Contains("label", StringComparison.OrdinalIgnoreCase));
        tools.Should().ContainSingle(tool =>
            tool.Name == "capture_all_screens"
            && tool.Title == "Capture All Screens"
            && tool.Description.Contains("monitors", StringComparison.OrdinalIgnoreCase));
        tools.Should().ContainSingle(tool =>
            tool.Name == "list_windows"
            && tool.Title == "List Windows"
            && tool.Description.Contains("windows", StringComparison.OrdinalIgnoreCase));
        tools.Should().ContainSingle(tool =>
            tool.Name == "capture_window"
            && tool.Title == "Capture Window"
            && tool.Description.Contains("CaptureKit window", StringComparison.OrdinalIgnoreCase));
    }
}
