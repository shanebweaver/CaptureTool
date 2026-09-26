using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using System.Diagnostics;
using System.Net.Http;

// Opt-in diagnostics. Every input is synthetic; raw responses here can never contain a user's captures.
internal static class MetadataStabilityChecks
{
    internal static async Task<int> RunAsync(IStorageService storage, string output, int rounds)
    {
        Directory.CreateDirectory(output);
        using var runtime = new FoundryRuntime(storage);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        var model = await runtime.ResolveAsync("phi-4-mini", lifetime.Token) ?? throw new InvalidOperationException("No CPU model.");
        if (!await model.IsCachedAsync(lifetime.Token)) throw new InvalidOperationException("This diagnostic requires the existing cached model; it never downloads models.");
        int failures = 0;
        string[] descriptions = [
            "An invoice from Northwind with a prominent invoice number, total, and payment deadline.",
            "A screenshot of a Windows development workspace. A dark code editor fills most of the screen, with a file tree on the left showing Program.cs, appsettings.json, and the project file. The editor shows a C# application startup method. A terminal panel at the bottom contains a red build error followed by command output. Tabs across the top include Program.cs and README.md. A toolbar offers Run and Debug actions, and a status bar shows the current branch and two problems. A small notification in the lower right says the build failed.",
            "A landscape photograph of a red wooden cabin beside a lake. Tall evergreen trees stand behind the cabin, mountains rise in the background, and clouds reflect on the water. A narrow wooden dock extends into the lake, with an empty blue canoe tied to the side. There are no visible people and no visible text."
        ];
        string[][] text = [
            ["Northwind invoice INV-1042", "Total USD 125.00", "Due date 2026-10-15"],
            ["Program.cs", "README.md", "using System.Text.Json;", "var builder = WebApplication.CreateBuilder(args);", "builder.Services.AddControllers();", "app.MapControllers();", "dotnet build", "error E1042: Missing configuration file appsettings.json", "Build FAILED.", "2 problems", "main", "Run", "Debug", "Terminal", "Output", "Build failed: see terminal for details."],
            []
        ];
        try
        {
            for (int round = 0; round < rounds; round++)
            for (int fixture = 0; fixture < descriptions.Length; fixture++)
            foreach (var capability in new[] { AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText })
            {
                var producer = new AnalyzerProvenance("fixture", "synthetic", "fixture", "1");
                var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "probe", Guid.NewGuid(), [
                    new(new DescriptionMetadata([new(descriptions[fixture])]), producer, DateTimeOffset.UtcNow, "probe"),
                    new(new TextRecognitionMetadata(text[fixture].Select(value => new RecognizedText(value))), producer, DateTimeOffset.UtcNow, "probe")
                ]);
                var input = MetadataProcessorInput.Create(record, MetadataEnrichmentConfiguration.CreateSemantic("probe", capability));
                var timer = Stopwatch.StartNew();
                await using (await runtime.AcquireModelAsync(model, lifetime.Token))
                {
                    try
                    {
                        var endpoint = await runtime.StartChatServiceAsync(lifetime.Token);
                        using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                            { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = FoundryMetadataProtocol.MaximumResponseBytes };
                        string label = $"round-{round + 1}-fixture-{fixture + 1}-{capability.Name}";
                        int attempts = 0;
                        var outcome = await FoundryMetadataGeneration.GenerateAsync(model.Id, input,
                            new(input.Descriptor.Id, "synthetic", model.Id, input.Descriptor.Version), async request =>
                            {
                                attempts++;
                                using var body = new ByteArrayContent(request);
                                body.Headers.ContentType = new("application/json");
                                using var response = await http.PostAsync(endpoint, body, CancellationToken.None);
                                byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
                                await File.WriteAllBytesAsync(Path.Combine(output, label + $"-attempt-{attempts}.json"), bytes, lifetime.Token);
                                response.EnsureSuccessStatusCode();
                                return bytes;
                            }, lifetime.Token);
                        bool passed = outcome.Kind == AnalyzerOutcomeKind.Succeeded && timer.Elapsed <= input.Descriptor.Limits.ExecutionTimeout;
                        if (!passed) failures++;
                        else _ = record.WithResult(new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, "probe", derivation: input.Derivation));
                        Console.WriteLine($"{label}: {outcome.Kind} {outcome.FailureCode} ({attempts} attempt(s), {timer.Elapsed.TotalSeconds:F1}s){(passed ? string.Empty : " FAILED")}");
                    }
                    finally { await runtime.StopServiceAsync(); }
                }
                // First round exercises an unload/reload on every action; later rounds reuse a resident model.
                if (round == 0) await runtime.ReleaseAsync();
            }
        }
        finally { await runtime.ReleaseAsync(); }
        Console.WriteLine($"Completed {rounds * descriptions.Length * 2} calls; {failures} rejected responses.");
        return failures == 0 ? 0 : 1;
    }
}
