using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Infrastructure.Analysis.Windows.Foundry;

/// <summary>One bounded correction within the caller's existing deadline and native model lease.</summary>
internal static class FoundryMetadataGeneration
{
    internal static async Task<AnalyzerOutcome> GenerateAsync(string modelId, MetadataProcessorInput input,
        AnalyzerProvenance producer, Func<byte[], Task<byte[]>> complete, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            byte[] request = FoundryMetadataProtocol.CreateRequest(modelId, input, correction: attempt != 0);
            // The transport retains native ownership until it actually finishes. Cancellation
            // fences publication and prevents a correction, never overlaps another invocation.
            byte[] response = await complete(request).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            AnalyzerOutcome outcome = FoundryMetadataProtocol.ParseResponse(response, modelId, input, producer);
            if (attempt == 1 || !FoundryMetadataProtocol.CanCorrect(outcome))
            {
                return outcome;
            }
        }
    }
}
