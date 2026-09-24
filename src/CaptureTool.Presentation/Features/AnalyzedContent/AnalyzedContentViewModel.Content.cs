using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Presentation.Features.AnalyzedContent;

public sealed partial class AnalyzedContentViewModel
{
    private static string DescribeProperties(MediaPropertiesV1? properties)
    {
        if (properties == null) { return string.Empty; }
        var values = new List<string>();
        if (properties.PixelSize is PixelSize size) { values.Add($"{size.Width} × {size.Height} px"); }
        if (properties.Duration is TimeSpan duration) { values.Add(AnalyzedContentItemViewModel.FormatTimecode(duration)); }
        values.AddRange(new[] { properties.MimeType, properties.Container, properties.VideoCodec, properties.AudioCodec }.OfType<string>().Where(value => value.Length > 0));
        if (properties.AudioChannelCount is int channels) { values.Add($"{channels} ch"); }
        if (properties.SampleRateHz is int sampleRate) { values.Add($"{sampleRate:N0} Hz"); }
        if (properties.BitRate is long bitRate) { values.Add($"{bitRate:N0} bps"); }
        if (properties.FrameRate is double frameRate) { values.Add($"{frameRate:0.##} fps"); }
        return string.Join(Environment.NewLine, values);
    }

    private sealed record SectionDefinition(CaptureMemoryMatchKind Kind, string FullText, IEnumerable<CaptureAnalyzedPassage> LegacyPassages);
    private static IEnumerable<SectionDefinition> Definitions(CaptureMetadataViewSnapshot snapshot)
    {
        // Payload-only snapshots remain supported for existing adapters; canonical services supply identities in Passages.
        if (snapshot.MediaKind == CaptureMediaKind.Image)
        {
            var ocr = snapshot.ImageText;
            var lines = ocr?.Regions.SelectMany(region => region.Lines).Select((line, index) => new CaptureAnalyzedPassage(
                CaptureMemoryMatchKind.OcrText, $"image:{index}", line.Text, PixelBounds: new(line.Bounds.X, line.Bounds.Y,
                    line.Bounds.Width, line.Bounds.Height, (int)ocr.RasterSize.Width, (int)ocr.RasterSize.Height))).ToArray() ?? [];
            yield return new(CaptureMemoryMatchKind.OcrText, ocr?.FullText ?? string.Empty,
                lines.Length > 0 ? lines : TextFallback(CaptureMemoryMatchKind.OcrText, ocr?.FullText));
            yield return new(CaptureMemoryMatchKind.ImageDescription, snapshot.ImageDescription?.Description ?? string.Empty,
                TextFallback(CaptureMemoryMatchKind.ImageDescription, snapshot.ImageDescription?.Description));
        }
        else
        {
            var transcript = snapshot.SpeechTranscript;
            yield return new(CaptureMemoryMatchKind.SpeechTranscript, transcript?.FullText ?? string.Empty,
                transcript?.Segments.Count > 0 ? transcript.Segments.Select((segment, index) => new CaptureAnalyzedPassage(
                    CaptureMemoryMatchKind.SpeechTranscript, $"transcript:{index}", segment.Text, segment.StartTime,
                    segment.EndTime, SecondaryLabel: segment.SpeakerLabel ?? transcript.LanguageTag)) :
                    TextFallback(CaptureMemoryMatchKind.SpeechTranscript, transcript?.FullText));
            if (snapshot.MediaKind == CaptureMediaKind.Video)
            {
                yield return new(CaptureMemoryMatchKind.VideoOcrText, snapshot.VideoText?.FullText ?? string.Empty,
                    snapshot.VideoText?.Observations.Select((observation, index) => new CaptureAnalyzedPassage(
                        CaptureMemoryMatchKind.VideoOcrText, $"screen:{index}", observation.Text, observation.StartTime, observation.EndTime)) ?? []);
                yield return new(CaptureMemoryMatchKind.VideoDescription, snapshot.VideoDescription?.FullText ?? string.Empty,
                    snapshot.VideoDescription?.Observations.Select((observation, index) => new CaptureAnalyzedPassage(
                        CaptureMemoryMatchKind.VideoDescription, $"description:{index}", observation.Description, observation.StartTime, observation.EndTime)) ?? []);
            }
        }
    }
    private static IEnumerable<CaptureAnalyzedPassage> TextFallback(CaptureMemoryMatchKind kind, string? text) =>
        string.IsNullOrWhiteSpace(text) ? [] : [new(kind, $"{(int)kind}:-1", text)];
}
