using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Application.DependencyInjection;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.DependencyInjection;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using CaptureTool.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Net.Http;
using CaptureTool.Infrastructure.Analysis.Windows.Media;

// Opt-in, cached models only. Exercises actual image decoding, OCR, vision and text
// adapters using generated screenshots; no user capture or application policy is read.
internal static class ScreenshotChecks
{
    internal static async Task<int> RunAsync(IStorageService storage, string output, bool classificationOnly = false)
    {
        Directory.CreateDirectory(output);
        using var services = new ServiceCollection().AddGenericServices().AddApplicationServices().AddSingleton(storage)
            .AddWindowsAnalysisProviders(MetadataEnrichmentConfiguration.SemanticModels).BuildServiceProvider();
        var runtime = services.GetRequiredService<FoundryRuntime>();
        using var lifetime = new CancellationTokenSource(TimeSpan.FromMinutes(12));
        foreach (string alias in new[] { "qwen3.5-0.8b", "phi-4-mini" })
        {
            var model = await runtime.ResolveAsync(alias, lifetime.Token);
            if (model == null || !await model.IsCachedAsync(lifetime.Token))
                throw new InvalidOperationException("This diagnostic requires cached vision and Phi models; it never downloads models.");
        }
        var analyzers = services.GetServices<IMediaAnalyzer>().ToDictionary(value => value.Descriptor.Id);
        var processors = services.GetServices<IMetadataProcessor>().ToDictionary(value => value.Descriptor.Id);
        var plan = CaptureAnalysisConfiguration.CreateDefault().Plans.Single(value => value.MediaKind == AnalysisMediaKind.Image);
        int failures = 0;
        try
        {
            foreach (bool dense in new[] { true, false })
            {
                string label = dense ? "dense" : "invoice";
                string path = Path.Combine(output, label + ".png");
                using (var bitmap = new Bitmap(dense ? 1920 : 1000, dense ? 1080 : 400))
                using (var graphics = Graphics.FromImage(bitmap))
                using (var font = new Font("Arial", dense ? 20 : 30))
                {
                    graphics.Clear(dense ? Color.FromArgb(30, 30, 30) : Color.White);
                    if (dense)
                    {
                        string[] lines = ["Project CaptureTool - Program.cs - README.md - Run - Debug", "using System.Text.Json;", "var builder = WebApplication.CreateBuilder(args);", "builder.Services.AddControllers();", "app.MapControllers();", "Terminal: dotnet build", "error E1042: Missing configuration file appsettings.json", "Build FAILED. 2 problems. Branch: main."];
                        for (int i = 0; i < 24; i++) graphics.DrawString(lines[i % lines.Length], font, Brushes.White, 30, 30 + i * 40);
                    }
                    else graphics.DrawString("Northwind invoice INV-1042\nTotal USD 125.00\nDue date 2026-10-15", font, Brushes.Black, 35, 35);
                    bitmap.Save(path, ImageFormat.Png);
                }
                var revision = new SourceRevision(Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(path, lifetime.Token))));
                var capture = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, revision, plan.Version, Guid.NewGuid(), []);
                IReadOnlyList<AnalysisCapability> capabilities = classificationOnly
                    ? [AnalysisCapability.TextRecognition, AnalysisCapability.Description, AnalysisCapability.CaptureClassification]
                    : [AnalysisCapability.TextRecognition, AnalysisCapability.Description, AnalysisCapability.CaptureName,
                        AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText, AnalysisCapability.CaptureClassification];
                foreach (var capability in capabilities)
                {
                    var timer = Stopwatch.StartNew();
                    AnalyzerOutcome? outcome = null;
                    MetadataProcessorInput? snapshot = null;
                    var step = plan.Steps.Single(value => value.Capability == capability);
                    foreach (string id in step.Candidates)
                    {
                        using var budget = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        budget.CancelAfter(step.ExecutionTimeout);
                        if (analyzers.TryGetValue(id, out var analyzer))
                        {
                            var available = await analyzer.GetAvailabilityAsync(AnalysisMediaKind.Image, "en", budget.Token);
                            if (available == AnalyzerAvailability.PreparationRequired) available = await analyzer.PrepareAsync(null, budget.Token);
                            Console.WriteLine($"{label} {id} readiness: {available}");
                            if (available != AnalyzerAvailability.Ready) continue;
                            outcome = await analyzer.AnalyzeAsync(new(capture.CaptureId, AnalysisMediaKind.Image, revision, path, "en"), null, budget.Token);
                            if (id == "foundry-local-image-description" && outcome.Kind == AnalyzerOutcomeKind.Failed)
                            {
                                var model = (await runtime.ResolveAsync("qwen3.5-0.8b", budget.Token))!;
                                await using (await runtime.AcquireModelAsync(model, budget.Token))
                                {
                                    try
                                    {
                                        var endpoint = await runtime.StartVisionServiceAsync(budget.Token);
                                        using var bitmap = await WindowsAnalysisMedia.LoadImageAsync(path, budget.Token);
                                        byte[] encoded = await FoundryImageDescriptionAnalyzer.EncodeAsync(bitmap, budget.Token);
                                        await SendAsync(endpoint, FoundryVisionProtocol.CreateRequest(model.Id, encoded), label + "-vision-response.json");
                                    }
                                    finally { await runtime.StopServiceAsync(); }
                                }
                            }
                        }
                        else
                        {
                            var processor = processors[id];
                            var available = await processor.GetAvailabilityAsync(budget.Token);
                            if (available == AnalyzerAvailability.PreparationRequired) available = await processor.PrepareAsync(null, budget.Token);
                            Console.WriteLine($"{label} {id} readiness: {available}");
                            if (available != AnalyzerAvailability.Ready) continue;
                            snapshot = MetadataProcessorInput.Create(capture, processor.Descriptor);
                            await File.WriteAllTextAsync(Path.Combine(output, label + "-" + capability.Name + "-sources.json"),
                                CaptureTool.Infrastructure.Analysis.Windows.MetadataTextProtocol.CreateSources(snapshot), lifetime.Token);
                            if (id.StartsWith("foundry-phi4-mini-", StringComparison.Ordinal))
                            {
                                var model = (await runtime.ResolveAsync("phi-4-mini", budget.Token))!;
                                await using (await runtime.AcquireModelAsync(model, budget.Token))
                                {
                                    try
                                    {
                                        var endpoint = await runtime.StartChatServiceAsync(budget.Token);
                                        int attempt = 0;
                                        outcome = await FoundryMetadataGeneration.GenerateAsync(model.Id, snapshot,
                                            new(id, "foundry-local", model.Id, processor.Descriptor.Version),
                                            request => SendAsync(endpoint, request, label + "-" + capability.Name + $"-attempt-{++attempt}.json"), budget.Token);
                                    }
                                    finally { await runtime.StopServiceAsync(); }
                                }
                            }
                            else outcome = await processor.ProcessAsync(snapshot, budget.Token);
                        }
                        Console.WriteLine($"{label} {id}: {outcome.Kind} ({outcome.FailureCode}), {timer.Elapsed.TotalSeconds:F1}s");
                        if (outcome.Kind is AnalyzerOutcomeKind.Succeeded or AnalyzerOutcomeKind.ContentRejected or AnalyzerOutcomeKind.Cancelled) break;
                    }
                    if (outcome?.Kind != AnalyzerOutcomeKind.Succeeded) { failures++; continue; }
                    capture = capture.WithResult(new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, plan.Version, derivation: snapshot?.Derivation));
                    if (outcome.Payload is TextRecognitionMetadata ocr) Console.WriteLine($"{label}: {ocr.Regions.Count} OCR entries");
                    if (outcome.Payload is DescriptionMetadata description) Console.WriteLine($"{label} description: {description.Descriptions[0].Text}");
                    if (outcome.Payload is CaptureNameMetadata name) Console.WriteLine($"{label} name: {name.Suggestion?.Text}");
                    if (outcome.Payload is CaptureSynopsisMetadata synopsis) Console.WriteLine($"{label} summary: {string.Join(" ", synopsis.Summary.Select(item => item.Text))}");
                    if (outcome.Payload is ImageAltTextMetadata alt) Console.WriteLine($"{label} alt text: {alt.Suggestion?.Text}");
                    if (outcome.Payload is CaptureClassificationMetadata classification)
                    {
                        Console.WriteLine($"{label} category: {classification.Category}; topics: {string.Join(", ", classification.Topics.Select(topic => topic.Text))}");
                        if (dense ? classification.Category is not (CaptureCategory.Error or CaptureCategory.Code) : classification.Category != CaptureCategory.Document)
                        {
                            failures++;
                            Console.WriteLine($"{label}: classification does not match the synthetic fixture.");
                        }
                    }
                    string? generated = outcome.Payload switch
                    {
                        CaptureNameMetadata value => value.Suggestion?.Text ?? string.Empty,
                        CaptureSynopsisMetadata value => string.Join(" ", value.Summary.Select(item => item.Text)),
                        ImageAltTextMetadata value => value.Suggestion?.Text ?? string.Empty,
                        _ => null,
                    };
                    string[] expected = dense ? ["code", "build", "error", "configuration", "editor", "program"] : ["invoice", "Northwind"];
                    if (generated != null && (string.IsNullOrWhiteSpace(generated) || generated.Contains("library", StringComparison.OrdinalIgnoreCase) ||
                        dense && generated.Contains("Linux", StringComparison.OrdinalIgnoreCase) ||
                        !expected.Any(word => generated.Contains(word, StringComparison.OrdinalIgnoreCase))))
                    {
                        failures++;
                        Console.WriteLine($"{label}: generated content does not match the synthetic fixture.");
                    }
                }
                await runtime.ReleaseAsync();
            }
        }
        finally { await runtime.ReleaseAsync(); }
        Console.WriteLine($"Screenshot checks: {failures} failed stages.");
        return failures == 0 ? 0 : 1;

        async Task<byte[]> SendAsync(Uri endpoint, byte[] request, string name)
        {
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 65536 };
            using var body = new ByteArrayContent(request);
            body.Headers.ContentType = new("application/json");
            using var response = await http.PostAsync(endpoint, body, CancellationToken.None);
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
            await File.WriteAllBytesAsync(Path.Combine(output, name), bytes, lifetime.Token);
            response.EnsureSuccessStatusCode();
            return bytes;
        }
    }
}
