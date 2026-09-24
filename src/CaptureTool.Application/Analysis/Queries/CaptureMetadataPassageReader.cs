using CaptureTool.Application.Abstractions.Analysis.Memory;
using CaptureTool.Application.Abstractions.Edit.Metadata;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;

namespace CaptureTool.Application.Analysis.Queries;

/// <summary>The same canonical passage identities are used by search and every editor.</summary>
internal static class CaptureMetadataPassageReader
{
    public static IReadOnlyList<CaptureAnalyzedPassage> Read(CaptureAnalysisRecord record, bool fullText = false)
    {
        var passages = new List<CaptureAnalyzedPassage>();
        foreach (CapabilityAnalysis analysis in record.Analyses)
        {
            if (analysis.CanonicalResult is not { } result) { continue; }
            string id = result.ResultId.Value.ToString("N");
            switch (result.Payload)
            {
                case OcrDocumentV1 ocr:
                    if (fullText) { Add(CaptureMemoryMatchKind.OcrText, -1, ocr.FullText, combined: true); break; }
                    int lineIndex = 0;
                    foreach (var line in ocr.Regions.SelectMany(region => region.Lines))
                    {
                        Add(CaptureMemoryMatchKind.OcrText, lineIndex++, line.Text, bounds: new(
                            line.Bounds.X, line.Bounds.Y, line.Bounds.Width, line.Bounds.Height,
                            checked((int)ocr.RasterSize.Width), checked((int)ocr.RasterSize.Height)));
                    }
                    if (lineIndex == 0) { Add(CaptureMemoryMatchKind.OcrText, -1, ocr.FullText); }
                    break;
                case SpeechTranscriptV1 transcript:
                    if (fullText) { Add(CaptureMemoryMatchKind.SpeechTranscript, -1, transcript.FullText, combined: transcript.Segments.Count > 0); break; }
                    for (int index = 0; index < transcript.Segments.Count; index++)
                    {
                        var segment = transcript.Segments[index];
                        Add(CaptureMemoryMatchKind.SpeechTranscript, index, segment.Text, segment.StartTime,
                            segment.EndTime, label: string.Join(" · ", new[] { segment.SpeakerLabel, transcript.LanguageTag }
                                .Where(value => !string.IsNullOrWhiteSpace(value))));
                    }
                    if (transcript.Segments.Count == 0) { Add(CaptureMemoryMatchKind.SpeechTranscript, -1, transcript.FullText); }
                    break;
                case VideoOcrTrackV1 videoText:
                    if (fullText) { Add(CaptureMemoryMatchKind.VideoOcrText, -1, videoText.FullText, combined: videoText.Observations.Count > 0); break; }
                    for (int index = 0; index < videoText.Observations.Count; index++)
                    {
                        var observation = videoText.Observations[index];
                        Add(CaptureMemoryMatchKind.VideoOcrText, index, observation.Text, observation.StartTime, observation.EndTime);
                    }
                    if (videoText.Observations.Count == 0) { Add(CaptureMemoryMatchKind.VideoOcrText, -1, videoText.FullText); }
                    break;
                case VideoDescriptionTrackV1 videoDescription:
                    if (fullText) { Add(CaptureMemoryMatchKind.VideoDescription, -1, videoDescription.FullText, combined: videoDescription.Observations.Count > 0); break; }
                    for (int index = 0; index < videoDescription.Observations.Count; index++)
                    {
                        var observation = videoDescription.Observations[index];
                        Add(CaptureMemoryMatchKind.VideoDescription, index, observation.Description, observation.StartTime, observation.EndTime);
                    }
                    if (videoDescription.Observations.Count == 0) { Add(CaptureMemoryMatchKind.VideoDescription, -1, videoDescription.FullText); }
                    break;
                case ImageDescriptionV1 description:
                    Add(CaptureMemoryMatchKind.ImageDescription, 0, description.Description);
                    break;
            }

            void Add(CaptureMemoryMatchKind kind, int index, string text, TimeSpan? start = null,
                TimeSpan? end = null, CaptureMemoryPixelBounds? bounds = null, string? label = null, bool combined = false)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Consecutive sampled frames showing the same label are one occurrence.
                    if (kind is CaptureMemoryMatchKind.VideoOcrText or CaptureMemoryMatchKind.VideoDescription &&
                        passages.LastOrDefault() is { } previous && previous.MatchKind == kind &&
                        previous.Text == text && start.HasValue && previous.EndTime is TimeSpan previousEnd &&
                        start.Value <= previousEnd && end.HasValue)
                    {
                        passages[^1] = previous with { EndTime = end.Value > previousEnd ? end : previousEnd };
                        return;
                    }
                    passages.Add(new(kind, $"{id}:{index}", text, start, end, bounds, label, combined));
                }
            }
        }
        return passages.AsReadOnly();
    }
}
