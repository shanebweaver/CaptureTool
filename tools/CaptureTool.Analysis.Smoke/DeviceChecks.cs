using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Application.Abstractions.Storage;
using CaptureTool.Application.Analysis;
using CaptureTool.Domain;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Foundry;
using Microsoft.AI.Foundry.Local;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

// Explicit development diagnostic. Production provider selection remains unchanged.
internal static class DeviceChecks
{
    internal static async Task<int> ProbeAsync(IStorageService storage, string[] args)
    {
        using var runtime = new FoundryRuntime(storage);
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        _ = await runtime.ResolveAsync("phi-4-mini", budget.Token);
        var manager = FoundryLocalManager.Instance;
        foreach (var ep in manager.DiscoverEps())
            Console.WriteLine($"EP {ep.Name}: registered={ep.IsRegistered}");
        await PrepareEpAsync(manager, args, budget.Token);
        var catalog = await manager.GetCatalogAsync(budget.Token);
        foreach (string alias in new[] { "phi-4-mini", "qwen3.5-0.8b" })
        {
            var model = await catalog.GetModelAsync(alias, budget.Token);
            Console.WriteLine($"Alias {alias}: selected={model?.Id}");
            if (model == null) continue;
            foreach (var variant in model.Variants)
                Console.WriteLine($"Variant {variant.Id}: device={variant.Info.Runtime?.DeviceType}, ep={variant.Info.Runtime?.ExecutionProvider}, cached={await variant.IsCachedAsync(budget.Token)}");
        }
        return 0;
    }

    private static async Task PrepareEpAsync(FoundryLocalManager manager, string[] args, CancellationToken ct)
    {
        int prepare = Array.IndexOf(args, "--prepare-ep");
        if (prepare >= 0)
        {
            if (prepare + 1 == args.Length) throw new ArgumentException("Supply an execution provider name.");
            string ep = args[prepare + 1];
            if (!manager.DiscoverEps().Any(candidate => candidate.Name == ep))
                throw new ArgumentException("Request an execution provider returned by discovery.");
            int lastPercent = -1;
            var result = await manager.DownloadAndRegisterEpsAsync([ep], (name, percent) =>
            {
                if ((int)percent == lastPercent) return;
                lastPercent = (int)percent;
                Console.WriteLine($"Preparing {name}: {percent:F0}%");
            }, ct);
            Console.WriteLine($"EP preparation: success={result.Success}, status={result.Status}");
            if (!result.Success) throw new InvalidOperationException("Execution provider preparation failed.");
        }
    }

    internal static async Task<int> RunAsync(IStorageService storage, string output, string[] args)
    {
        int option = Array.IndexOf(args, "--device-check");
        if (option < 0 || option + 1 == args.Length) return 2;
        Directory.CreateDirectory(output);
        using var runtime = new FoundryRuntime(storage);
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        _ = await runtime.ResolveAsync("phi-4-mini", budget.Token);
        await PrepareEpAsync(FoundryLocalManager.Instance, args, budget.Token);
        var catalog = await FoundryLocalManager.Instance.GetCatalogAsync(budget.Token);
        var model = await catalog.GetModelVariantAsync(args[option + 1], budget.Token)
            ?? throw new ArgumentException("Request a model variant returned by --device-probe.");
        Console.WriteLine($"Model {model.Id}; device={model.Info.Runtime?.DeviceType}; ep={model.Info.Runtime?.ExecutionProvider}");
        if (!await model.IsCachedAsync(budget.Token))
        {
            if (!args.Contains("--prepare-model", StringComparer.Ordinal))
                throw new InvalidOperationException("Model is not cached. Use --prepare-model to download this specific variant.");
            int lastPercent = -1;
            await model.DownloadAsync(percent =>
            {
                if ((int)percent == lastPercent) return;
                lastPercent = (int)percent;
                Console.WriteLine($"Downloading model: {percent:F0}%");
            }, budget.Token);
        }
        int failures = 0;
        var timer = Stopwatch.StartNew();
        try
        {
            await using (await runtime.AcquireModelAsync(model, budget.Token, retainWhenIdle: true))
            {
                Console.WriteLine($"Model load: {timer.Elapsed.TotalSeconds:F3}s");
                timer.Restart();
                var endpoint = await runtime.StartChatServiceAsync(budget.Token);
                Console.WriteLine($"Listener start: {timer.Elapsed.TotalSeconds:F3}s");
                using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
                    { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = FoundryMetadataProtocol.MaximumResponseBytes };
                foreach (bool dense in new[] { false, true })
                {
                    string label = dense ? "dense" : "invoice";
                    string description = dense ? "A dark code editor with a terminal showing a build failure and missing configuration file." :
                        "A Northwind invoice with an invoice number, total amount, and payment deadline.";
                    string text = dense ? string.Join(" ", Enumerable.Repeat("Project CaptureTool Program.cs README.md Run Debug using System.Text.Json; var builder = WebApplication.CreateBuilder(args); dotnet build error E1042 Missing configuration file appsettings.json Build FAILED 2 problems Branch main", 3)) :
                        "Northwind invoice INV-1042 Total USD 125.00 Due date 2026-10-15";
                    var producer = new AnalyzerProvenance("fixture", "synthetic", "fixture", "1");
                    var record = new CaptureAnalysisRecord(CaptureId.New(), AnalysisMediaKind.Image, new(new string('a', 64)), "device-check", Guid.NewGuid(), [
                        new(new DescriptionMetadata([new(description)]), producer, DateTimeOffset.UtcNow, "device-check"),
                        new(new TextRecognitionMetadata(text.Split(' ').Select(word => new RecognizedText(word))), producer, DateTimeOffset.UtcNow, "device-check")
                    ]);
                    foreach (var capability in new[] { AnalysisCapability.CaptureName, AnalysisCapability.CaptureSynopsis, AnalysisCapability.ImageAltText })
                    {
                        var input = MetadataProcessorInput.Create(record, MetadataEnrichmentConfiguration.CreateSemantic("device-check", capability));
                        int attempts = 0;
                        timer.Restart();
                        var outcome = await FoundryMetadataGeneration.GenerateAsync(model.Id, input,
                            new(input.Descriptor.Id, "foundry-local", model.Id, input.Descriptor.Version), async request =>
                            {
                                attempts++;
                                using var body = new ByteArrayContent(request);
                                body.Headers.ContentType = new("application/json");
                                // Await native ownership even if the outer budget expires.
                                using var response = await http.PostAsync(endpoint, body, CancellationToken.None);
                                byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);
                                await File.WriteAllBytesAsync(Path.Combine(output, $"{label}-{capability.Name}-{attempts}.json"), bytes);
                                response.EnsureSuccessStatusCode();
                                using var json = JsonDocument.Parse(bytes);
                                Console.WriteLine($"{label} {capability.Name} usage: {json.RootElement.GetProperty("usage")}");
                                return bytes;
                            }, budget.Token);
                        Console.WriteLine($"{label} {capability.Name}: {outcome.Kind} ({outcome.FailureCode}), {timer.Elapsed.TotalSeconds:F3}s, {attempts} attempt(s)");
                        if (outcome.Kind != AnalyzerOutcomeKind.Succeeded) { failures++; continue; }
                        _ = record.WithResult(new(outcome.Payload!, outcome.Producer!, DateTimeOffset.UtcNow, "device-check", derivation: input.Derivation));
                        string generated = outcome.Payload switch
                        {
                            CaptureNameMetadata value => value.Suggestion?.Text ?? string.Empty,
                            CaptureSynopsisMetadata value => string.Join(" ", value.Summary.Select(item => item.Text)),
                            ImageAltTextMetadata value => value.Suggestion?.Text ?? string.Empty,
                            _ => string.Empty,
                        };
                        string[] expected = dense ? ["code", "build", "error", "configuration", "editor"] : ["invoice", "Northwind"];
                        Console.WriteLine($"{label} {capability.Name} synthetic output: {generated}");
                        if (!expected.Any(word => generated.Contains(word, StringComparison.OrdinalIgnoreCase))) failures++;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or FoundryLocalException)
        {
            Console.WriteLine($"Device check failed: {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine($"Synthetic response diagnostics: {output}");
            return 1;
        }
        finally
        {
            await runtime.StopServiceAsync();
            await runtime.ReleaseAsync();
        }
        Console.WriteLine($"Device checks: {failures} failed stages. Synthetic responses: {output}");
        return failures == 0 ? 0 : 1;
    }
}
