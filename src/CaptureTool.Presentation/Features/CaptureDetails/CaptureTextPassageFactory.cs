using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using CaptureTool.Application.Abstractions.Localization;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using System.Drawing;

namespace CaptureTool.Presentation.Features.CaptureDetails;

/// <summary>Converts editor text and saved analysis into the same selectable passage model.</summary>
internal static class CaptureTextPassageFactory
{
    public static IReadOnlyList<CaptureTextPassage> FromDocument(
        RecognizedTextDocument document, ILocalizationService localization)
    {
        string textLabel = localization.GetString("CaptureDetails_RecognizedText");
        string qrLabel = localization.GetString("CaptureDetails_QrCode");
        var regions = document.Regions.Select(region => new RecognizedText(
            region.Text,
            Normalize(region.Bounds, document.ImageSize),
            lineIndex: region.LineIndex >= 0 ? region.LineIndex : null,
            wordIndex: region.WordIndex >= 0 ? region.WordIndex : null)).ToArray();

        var passages = new List<CaptureTextPassage>();
        foreach (var group in CaptureTextGrouping.Create(regions))
        {
            passages.Add(CreateTextPassage($"editor:text:{group.FirstIndex}", textLabel, group, time: null));
        }

        if (regions.Length == 0 && document.QrCodes.Count == 0 && !string.IsNullOrWhiteSpace(document.Text))
        {
            passages.Add(new("editor:text", CaptureTextSource.ImageText, textLabel, document.Text, []));
        }

        int index = 0;
        foreach (var codes in document.QrCodes.GroupBy(code => code.Value, StringComparer.Ordinal))
        {
            var locations = codes.Select(code => new CaptureTextLocation(
                qrLabel, null, Normalize(code.Bounds, document.ImageSize)));
            passages.Add(new($"editor:qr:{index++}", CaptureTextSource.QrCode, qrLabel, codes.Key, locations));
        }

        return passages;
    }

    public static IReadOnlyList<CaptureTextPassage> FromRecord(
        CaptureAnalysisRecord record, ILocalizationService localization)
    {
        var passages = new List<CaptureTextPassage>();
        foreach (var result in record.Results)
        {
            switch (result.Payload)
            {
                case TextRecognitionMetadata text when record.MediaKind == AnalysisMediaKind.Video:
                    passages.AddRange(FromVideoText(result, text, localization.GetString("CaptureDetails_RecognizedText")));
                    break;
                case TextRecognitionMetadata text:
                    passages.AddRange(FromImageText(result, text, localization.GetString("CaptureDetails_RecognizedText")));
                    break;
                case TranscriptMetadata transcript:
                    passages.AddRange(FromTranscript(result, transcript, localization.GetString("CaptureDetails_Transcript")));
                    break;
                case QrCodeMetadata qr:
                    passages.AddRange(FromQrCodes(result, qr, record.MediaKind, localization.GetString("CaptureDetails_QrCode")));
                    break;
            }
        }

        return passages.OrderBy(passage => passage.SelectedLocation?.Time ?? TimeSpan.Zero).ToArray();
    }

    private static IEnumerable<CaptureTextPassage> FromVideoText(
        AnalysisResult result, TextRecognitionMetadata text, string label)
    {
        // Keep each word's original index so passage identity survives grouping by frame.
        var frames = text.Regions
            .Select((region, index) => (Region: region, Index: index))
            .GroupBy(item => item.Region.Timestamp)
            .OrderBy(group => group.Key);

        foreach (var frame in frames)
        {
            var words = frame.ToArray();
            var regions = words.Select(item => item.Region).ToArray();
            foreach (var group in CaptureTextGrouping.Create(regions))
            {
                string id = $"{result.ResultId}:{words[group.FirstIndex].Index}";
                yield return CreateTextPassage(id, label, group, frame.Key);
            }
        }
    }

    private static IEnumerable<CaptureTextPassage> FromImageText(
        AnalysisResult result, TextRecognitionMetadata text, string label)
    {
        foreach (var group in CaptureTextGrouping.Create(text.Regions))
        {
            string id = $"{result.ResultId}:{group.FirstIndex}";
            yield return CreateTextPassage(id, label, group, group.Regions[0].Timestamp);
        }
    }

    private static CaptureTextPassage CreateTextPassage(
        string id, string label, CaptureTextGrouping.Group group, TimeSpan? time) =>
        new(id, CaptureTextSource.ImageText, label, group.Text,
            [CreateLocation(time, group.Bounds, label)], group.Regions, group.Lines);

    private static IEnumerable<CaptureTextPassage> FromTranscript(
        AnalysisResult result, TranscriptMetadata transcript, string label)
    {
        for (int index = 0; index < transcript.Segments.Count; index++)
        {
            var segment = transcript.Segments[index];
            yield return new CaptureTextPassage(
                $"{result.ResultId}:{index}", CaptureTextSource.Speech, label, segment.Text,
                [CreateLocation(segment.Start, null, label)]);
        }
    }

    private static IEnumerable<CaptureTextPassage> FromQrCodes(
        AnalysisResult result, QrCodeMetadata qr, AnalysisMediaKind kind, string label)
    {
        var groups = qr.Codes
            .Select((code, index) => (Code: code, Index: index))
            .GroupBy(item => item.Code.Value, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var locations = group.Select(item => CreateLocation(
                item.Code.Timestamp, kind == AnalysisMediaKind.Image ? item.Code.Bounds : null, label));
            yield return new CaptureTextPassage(
                $"{result.ResultId}:{group.First().Index}", CaptureTextSource.QrCode, label, group.Key, locations);
        }
    }

    private static CaptureTextLocation CreateLocation(TimeSpan? time, NormalizedBounds? bounds, string label) =>
        new(time is { } value ? CaptureDetailsContent.Time(value) : label, time, bounds);

    private static NormalizedBounds? Normalize(RectangleF bounds, Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return null;
        }

        var clipped = RectangleF.Intersect(bounds, new(0, 0, imageSize.Width, imageSize.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return null;
        }

        double x = (double)clipped.X / imageSize.Width;
        double y = (double)clipped.Y / imageSize.Height;
        double width = Math.Min(1 - x, (double)clipped.Width / imageSize.Width);
        double height = Math.Min(1 - y, (double)clipped.Height / imageSize.Height);
        return new(x, y, width, height);
    }
}
