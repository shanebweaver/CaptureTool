using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
namespace CaptureTool.Infrastructure.Analysis.Windows;

/// <summary>Reads local filesystem/container properties without decoding pixels, sampling, or acquiring models.</summary>
internal sealed class FileDetailsAnalyzer(CaptureTool.Application.Abstractions.Library.CaptureDetails.IMediaFileDetailsReader? files = null) : IMediaAnalyzer
{
    public MediaAnalyzerDescriptor Descriptor { get; } = new("windows-file-details", AnalysisCapability.FileDetails,
        [AnalysisMediaKind.Image, AnalysisMediaKind.Audio, AnalysisMediaKind.Video]);

    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Descriptor.SupportedMedia.Contains(kind) ? AnalyzerAvailability.Ready : AnalyzerAvailability.Unsupported);
    }

    public Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(AnalyzerAvailability.Ready);
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken ct)
    {
        if (!Descriptor.SupportedMedia.Contains(input.MediaKind))
            return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-media");
        FileDetailsMetadata? properties = await (files ?? new WindowsMediaFileDetailsReader())
            .ReadAsync(input.SourcePath, input.MediaKind, input.CapturedAt, ct).ConfigureAwait(false);
        return properties == null
            ? AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "file-unavailable")
            : AnalyzerOutcome.Success(properties, new(Descriptor.Id, "windows", "file-properties", "1"));
    }
}
