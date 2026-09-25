using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using Microsoft.AI.Foundry.Local;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

internal sealed class FoundryMetadataProcessor(MetadataProcessorDescriptor descriptor, string alias, FoundryRuntime runtime) : IMetadataProcessor
{
    private IModel? _model;
    public MetadataProcessorDescriptor Descriptor { get; } = descriptor;

    public async ValueTask<AnalyzerAvailability> GetAvailabilityAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64)) return AnalyzerAvailability.Unsupported;
        return _model != null && await _model.IsCachedAsync(ct).ConfigureAwait(false) ? AnalyzerAvailability.Ready : AnalyzerAvailability.PreparationRequired;
    }

    public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        _model = await runtime.ResolveAsync(alias, ct).ConfigureAwait(false);
        if (_model?.Info.Task is not ("chat-completion" or "vision-language-chat"))
        {
            _model = null;
            return AnalyzerAvailability.Unsupported;
        }
        if (!await _model.IsCachedAsync(ct).ConfigureAwait(false))
            await _model.DownloadAsync(value => progress?.Report(new(AnalysisProgressStage.Preparing,
                double.IsFinite(value) ? Math.Clamp(value / 100d, 0, 1) : null)), ct).ConfigureAwait(false);
        progress?.Report(new(AnalysisProgressStage.Preparing, 1));
        return AnalyzerAvailability.Ready;
    }

    public async Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Descriptor.Matches(input.Descriptor)) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-contract-mismatch");
        if (input.Entries.Count == 0) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-input-empty");
        IModel? model = _model;
        if (model == null) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.TemporarilyUnavailable, "model-not-prepared");
        bool serving = false;
        try
        {
            byte[] request = FoundryMetadataProtocol.CreateRequest(model.Id, input);
            await model.LoadAsync(ct).ConfigureAwait(false);
            Uri endpoint = await runtime.StartChatServiceAsync(ct).ConfigureAwait(false);
            serving = true;
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
            { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = FoundryMetadataProtocol.MaximumResponseBytes };
            using var content = new ByteArrayContent(request);
            content.Headers.ContentType = new("application/json");
            ct.ThrowIfCancellationRequested();
            // Keep native ownership until inference ends, even if its caller has timed out or revoked consent.
            // The shared worker prevents subsequent provider invocation while this task is still running.
            using var response = await http.PostAsync(endpoint, content, CancellationToken.None).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (!response.IsSuccessStatusCode) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "text-response-failed");
            byte[] bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return FoundryMetadataProtocol.ParseResponse(bytes, model.Id, input,
                new(Descriptor.Id, "foundry-local", model.Id, Descriptor.Version, model.Info.Version.ToString(CultureInfo.InvariantCulture)));
        }
        finally
        {
            try { if (serving) await runtime.StopServiceAsync().ConfigureAwait(false); }
            finally { await model.UnloadAsync(CancellationToken.None).ConfigureAwait(false); }
        }
    }
}
