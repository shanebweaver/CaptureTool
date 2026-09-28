using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

/// <summary>Deterministic provider for isolated desktop tests; never downloads or invokes device models.</summary>
internal sealed class UiTestMediaAnalyzer(MediaAnalyzerDescriptor descriptor) : IMediaAnalyzer
{
    public MediaAnalyzerDescriptor Descriptor => descriptor;
    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct) =>
        ValueTask.FromResult(AnalyzerAvailability.Ready);
    public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct) =>
        Task.FromResult(AnalyzerAvailability.Ready);
    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        if (descriptor.Capability == AnalysisCapability.FileDetails)
            return AnalyzerOutcome.Success(new FileDetailsMetadata(input.MediaKind, "fixture.png", 123, "image/png",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, input.CapturedAt,
                image: UiTestTextFixture.Current is { } fixture ? new(new((uint)fixture.Width, (uint)fixture.Height), 96, 96) :
                    UiTestLaunchOptions.DetailsFixture && input.MediaKind == AnalysisMediaKind.Image ? new(new(1200, 800), 96, 96) : null), new(descriptor.Id, "ui-test", "fixture", "1"));
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        if (descriptor.Capability == AnalysisCapability.QrCodeDetection)
            return AnalyzerOutcome.Success(new QrCodeMetadata(UiTestLaunchOptions.DetailsFixture && UiTestTextFixture.Current == null
                ? [new("https://example.com/invoice", new(.1, .1, .2, .2))] : []), new(descriptor.Id, "ui-test", "fixture", "1"));
        if (descriptor.Capability == AnalysisCapability.TextRecognition)
            return AnalyzerOutcome.Success(new TextRecognitionMetadata(UiTestTextFixture.Current?.SavedText() ??
                (UiTestLaunchOptions.DetailsFixture ? TextFixture(input.MediaKind) : [])), new(descriptor.Id, "ui-test", "fixture", "1"));
        if (descriptor.Capability == AnalysisCapability.Transcription && UiTestLaunchOptions.DetailsFixture)
            return AnalyzerOutcome.Success(new TranscriptMetadata("en", [new("First spoken passage", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)),
                new("Second spoken passage", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4))]), new(descriptor.Id, "ui-test", "fixture", "1"));
        if (descriptor.Capability == AnalysisCapability.Description)
            return AnalyzerOutcome.Success(new DescriptionMetadata([new("UI fixture capture")]), new(descriptor.Id, "ui-test", "fixture", "1"));
        return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "ui-test-media");
    }

    private static IEnumerable<RecognizedText> TextFixture(AnalysisMediaKind kind)
    {
        string[] lines = ["Contoso invoice. Reference: INV-2048. Total USD 125.00. Due 2026-10-15.",
            "Contact billing@example.com or visit https://example.com/invoice."];
        for (int i = 0; i < lines.Length; i++)
        {
            if (kind != AnalysisMediaKind.Image)
            {
                yield return new(lines[i], timestamp: TimeSpan.FromSeconds(i + 1));
                continue;
            }
            // Match real providers: word-level regions, not preassembled display passages.
            double x = .1;
            int wordIndex = 0;
            foreach (string word in lines[i].Split(' '))
            {
                double width = word.Length * .007;
                yield return new(word, new(x, i == 0 ? .15 : .5, width, .035), lineIndex: i, wordIndex: wordIndex++);
                x += width + .007;
            }
        }
    }
}
