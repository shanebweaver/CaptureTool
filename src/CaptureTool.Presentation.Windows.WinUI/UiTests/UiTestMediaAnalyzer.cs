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
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        if (descriptor.Capability == AnalysisCapability.QrCodeDetection)
            return AnalyzerOutcome.Success(new QrCodeMetadata([]), new(descriptor.Id, "ui-test", "fixture", "1"));
        if (descriptor.Capability == AnalysisCapability.TextRecognition)
            return AnalyzerOutcome.Success(new TextRecognitionMetadata([]), new(descriptor.Id, "ui-test", "fixture", "1"));
        if (descriptor.Capability == AnalysisCapability.Description)
            return AnalyzerOutcome.Success(new DescriptionMetadata([new("UI fixture capture")]), new(descriptor.Id, "ui-test", "fixture", "1"));
        return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "ui-test-media");
    }
}
