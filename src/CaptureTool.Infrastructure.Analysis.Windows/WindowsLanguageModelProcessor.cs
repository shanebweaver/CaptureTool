using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using Microsoft.Windows.AI.Text;

namespace CaptureTool.Infrastructure.Analysis.Windows;

internal sealed class WindowsLanguageModelProcessor(MetadataProcessorDescriptor descriptor,
    IWindowsLanguageModelClient client) : IMetadataProcessor
{
    public MetadataProcessorDescriptor Descriptor { get; } = descriptor;

    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(client.GetAvailability());
    }

    public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(new(AnalysisProgressStage.Preparing));
        var ready = await client.PrepareAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (ready == AnalyzerAvailability.Ready)
        {
            progress?.Report(new(AnalysisProgressStage.Preparing, 1));
        }

        return ready;
    }

    public async Task<AnalyzerOutcome> ProcessAsync(MetadataProcessorInput input, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Descriptor.Matches(input.Descriptor))
        {
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-contract-mismatch");
        }

        if (input.Entries.Count == 0)
        {
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "metadata-input-empty");
        }

        string sources = MetadataTextProtocol.CreateSources(input);
        try
        {
            // Preparation also checks LAF access on already-installed models. No user
            // content is passed until Windows has confirmed availability.
            var ready = await client.PrepareAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (ready != AnalyzerAvailability.Ready)
            {
                return AnalyzerOutcome.Unsuccessful(ready == AnalyzerAvailability.Unsupported
                ? AnalyzerOutcomeKind.Unsupported : AnalyzerOutcomeKind.TemporarilyUnavailable, "windows-language-model-unavailable");
            }

            using var session = await client.CreateSessionAsync(ct).ConfigureAwait(false);
            for (int attempt = 0; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                string instructions = MetadataTextProtocol.CreateInstructions(input, correction: attempt != 0);
                var response = await session.GenerateAsync(instructions, sources).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                AnalyzerOutcome result = response.Status == LanguageModelResponseStatus.Complete
                    ? MetadataTextProtocol.ParseText(response.Text, input, new(Descriptor.Id, "microsoft-windows", "windows-language-model", Descriptor.Version))
                    : Rejected(response.Status);
                if (attempt == 1 || !MetadataTextProtocol.CanCorrect(result))
                {
                    return result;
                }
            }
        }
        catch (Exception exception) when (WindowsLanguageModelClient.IsUnavailable(exception))
        {
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, WindowsLanguageModelClient.IsAccessDenied(exception)
                ? "windows-language-model-access-denied" : "windows-language-model-unavailable");
        }
    }

    internal static AnalyzerOutcome Rejected(LanguageModelResponseStatus status) => status switch
    {
        LanguageModelResponseStatus.BlockedByPolicy or LanguageModelResponseStatus.PromptBlockedByContentModeration or
            LanguageModelResponseStatus.ResponseBlockedByContentModeration => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.ContentRejected, "windows-text-response-blocked"),
        LanguageModelResponseStatus.PromptLargerThanContext => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "windows-text-context-limit"),
        LanguageModelResponseStatus.UnsupportedLanguage => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "windows-text-language-unsupported"),
        _ => AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "windows-text-response-failed"),
    };
}
