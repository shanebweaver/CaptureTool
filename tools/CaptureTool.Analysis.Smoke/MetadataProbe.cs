using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using System.Net.Http;

// Explicit development diagnostic using one hard-coded synthetic invoice. Never reads captures.
internal static class MetadataProbe
{
    internal static async Task<int> RunAsync(IStorageService storage, string output, string alias)
    {
        using var runtime = new FoundryRuntime(storage);
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var model = await runtime.ResolveAsync(alias, budget.Token) ?? throw new InvalidOperationException("No CPU model.");
        if (!await model.IsCachedAsync(budget.Token)) await model.DownloadAsync(null, budget.Token);
        var source = new AnalysisResult(new TextRecognitionMetadata([new("Northwind invoice INV-1042. Total USD 125.00. Due date 2026-10-15.")]),
            new("fixture", "synthetic", "fixture", "1"), DateTimeOffset.UtcNow, "probe");
        var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "probe", Guid.NewGuid(), [source]);
        foreach (var capability in new[] { AnalysisCapability.CaptureSynopsis, AnalysisCapability.CaptureClassification })
        {
            var input = MetadataProcessorInput.Create(record, MetadataEnrichmentConfiguration.CreateSemantic("probe", capability));
            await model.LoadAsync(budget.Token);
            try
            {
                var endpoint = await runtime.StartChatServiceAsync(budget.Token);
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                    { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = FoundryMetadataProtocol.MaximumResponseBytes };
                using var body = new ByteArrayContent(FoundryMetadataProtocol.CreateRequest(model.Id, input));
                body.Headers.ContentType = new("application/json");
                using var response = await http.PostAsync(endpoint, body, CancellationToken.None);
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
                await File.WriteAllBytesAsync(Path.Combine(output, capability.Name + "-synthetic-probe.json"), bytes, budget.Token);
                Console.WriteLine(capability.Name + ": " + response.StatusCode);
            }
            finally { await runtime.StopServiceAsync(); await model.UnloadAsync(CancellationToken.None); }
        }
        return 0;
    }
}
