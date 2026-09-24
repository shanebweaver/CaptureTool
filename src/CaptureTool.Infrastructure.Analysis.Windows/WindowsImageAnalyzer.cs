using CaptureTool.Application.Abstractions.Analysis;
using CaptureTool.Domain.Analysis;
using CaptureTool.Domain.Analysis.Payloads;
using CaptureTool.Infrastructure.Analysis.Windows.Media;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.ContentSafety;
using Microsoft.Windows.AI.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using RecognizedText = CaptureTool.Domain.Analysis.Payloads.RecognizedText;

namespace CaptureTool.Infrastructure.Analysis.Windows;

internal enum WindowsImageModel { Ocr, LegacyOcr, Description }

internal sealed class WindowsImageAnalyzer(string id, WindowsImageModel model, AnalysisMediaKind mediaKind,
    WindowsAnalysisMedia media) : IMediaAnalyzer
{
    private const int MaximumRegions = 50000;
    public MediaAnalyzerDescriptor Descriptor { get; } = new(id,
        model == WindowsImageModel.Description ? AnalysisCapability.Description : AnalysisCapability.TextRecognition, [mediaKind]);

    public ValueTask<AnalyzerAvailability> GetAvailabilityAsync(AnalysisMediaKind kind, string? language, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (kind != mediaKind || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, model == WindowsImageModel.LegacyOcr ? 17763 : 26100))
            return ValueTask.FromResult(AnalyzerAvailability.Unsupported);
        try
        {
            if (model == WindowsImageModel.LegacyOcr)
                return ValueTask.FromResult(CreateLegacy(language) == null ? AnalyzerAvailability.Unsupported : AnalyzerAvailability.Ready);
            return ValueTask.FromResult(MapReady(model == WindowsImageModel.Ocr ? TextRecognizer.GetReadyState() : ImageDescriptionGenerator.GetReadyState()));
        }
        catch (Exception) { return ValueTask.FromResult(AnalyzerAvailability.Unsupported); }
    }

    public async Task<AnalyzerAvailability> PrepareAsync(IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new(AnalysisProgressStage.Preparing));
        if (model == WindowsImageModel.LegacyOcr) return AnalyzerAvailability.Ready;
        AIFeatureReadyResult ready = model == WindowsImageModel.Ocr
            ? await TextRecognizer.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false)
            : await ImageDescriptionGenerator.EnsureReadyAsync().AsTask(cancellationToken).ConfigureAwait(false);
        progress?.Report(new(AnalysisProgressStage.Preparing, 1));
        return ready.Status == AIFeatureReadyResultState.Success ? AnalyzerAvailability.Ready : AnalyzerAvailability.TemporarilyUnavailable;
    }

    public async Task<AnalyzerOutcome> AnalyzeAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        try { return await AnalyzeCoreAsync(input, progress, cancellationToken).ConfigureAwait(false); }
        catch (InvalidAnalysisMediaException) { return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.InvalidSource, "invalid-media"); }
    }

    private async Task<AnalyzerOutcome> AnalyzeCoreAsync(AnalysisInput input, IProgress<AnalysisProgress>? progress, CancellationToken cancellationToken)
    {
        if (input.MediaKind != mediaKind) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Unsupported, "unsupported-media");
        List<RecognizedText> text = [];
        List<MediaDescription> descriptions = [];
        if (mediaKind == AnalysisMediaKind.Image)
        {
            using SoftwareBitmap bitmap = await WindowsAnalysisMedia.LoadImageAsync(input.SourcePath, cancellationToken).ConfigureAwait(false);
            AnalyzerOutcomeKind status = await AnalyzeFrameAsync(bitmap, null, input.Language, text, descriptions, cancellationToken).ConfigureAwait(false);
            if (status != AnalyzerOutcomeKind.Succeeded) return AnalyzerOutcome.Unsuccessful(status, "windows-image-rejected");
        }
        else
        {
            int maximum = model == WindowsImageModel.Description ? 8 : 64;
            int count = 0;
            await foreach (var frame in media.ReadFramesAsync(input.SourcePath, maximum,
                TimeSpan.FromSeconds(model == WindowsImageModel.Description ? 30 : 5), cancellationToken).ConfigureAwait(false))
            {
                AnalyzerOutcomeKind status = await AnalyzeFrameAsync(frame.Bitmap, frame.Timestamp, input.Language, text, descriptions, cancellationToken).ConfigureAwait(false);
                if (status != AnalyzerOutcomeKind.Succeeded) return AnalyzerOutcome.Unsuccessful(status, "windows-frame-rejected");
                if (text.Count > MaximumRegions) return AnalyzerOutcome.Unsuccessful(AnalyzerOutcomeKind.Failed, "output-limit");
                progress?.Report(new(AnalysisProgressStage.Analyzing, ++count / (double)maximum));
            }
        }
        return AnalyzerOutcome.Success(model == WindowsImageModel.Description ? new DescriptionMetadata(descriptions) : new TextRecognitionMetadata(text),
            new(id, "microsoft-windows", model switch { WindowsImageModel.Ocr => "windows-ai-text-recognizer",
                WindowsImageModel.LegacyOcr => "windows-media-ocr", _ => "windows-ai-image-description" }, "1"));
    }

    private async Task<AnalyzerOutcomeKind> AnalyzeFrameAsync(SoftwareBitmap bitmap, TimeSpan? timestamp, string? language,
        List<RecognizedText> text, List<MediaDescription> descriptions, CancellationToken ct)
    {
        if (model == WindowsImageModel.LegacyOcr)
        {
            OcrEngine? engine = CreateLegacy(language);
            if (engine == null) return AnalyzerOutcomeKind.Unsupported;
            OcrResult recognized = await engine.RecognizeAsync(bitmap).AsTask(ct).ConfigureAwait(false);
            foreach (OcrWord word in recognized.Lines.SelectMany(line => line.Words))
            {
                if (string.IsNullOrWhiteSpace(word.Text)) continue;
                var bounds = word.BoundingRect;
                text.Add(new(word.Text, Bounds(bounds.X, bounds.Y, bounds.Right, bounds.Bottom, bitmap), timestamp));
                if (text.Count > MaximumRegions) return AnalyzerOutcomeKind.Failed;
            }
        }
        else
        {
            using ImageBuffer image = ImageBuffer.CreateForSoftwareBitmap(bitmap);
            if (model == WindowsImageModel.Ocr)
            {
                using TextRecognizer recognizer = await TextRecognizer.CreateAsync().AsTask(ct).ConfigureAwait(false);
                var recognized = await recognizer.RecognizeTextFromImageAsync(image).AsTask(ct).ConfigureAwait(false);
                foreach (RecognizedWord word in recognized.Lines.SelectMany(line => line.Words))
                {
                    if (string.IsNullOrWhiteSpace(word.Text)) continue;
                    var box = word.BoundingBox;
                    double left = Math.Min(Math.Min(box.TopLeft.X, box.TopRight.X), Math.Min(box.BottomLeft.X, box.BottomRight.X));
                    double top = Math.Min(Math.Min(box.TopLeft.Y, box.TopRight.Y), Math.Min(box.BottomLeft.Y, box.BottomRight.Y));
                    double right = Math.Max(Math.Max(box.TopLeft.X, box.TopRight.X), Math.Max(box.BottomLeft.X, box.BottomRight.X));
                    double bottom = Math.Max(Math.Max(box.TopLeft.Y, box.TopRight.Y), Math.Max(box.BottomLeft.Y, box.BottomRight.Y));
                    text.Add(new(word.Text, Bounds(left, top, right, bottom, bitmap), timestamp));
                    if (text.Count > MaximumRegions) return AnalyzerOutcomeKind.Failed;
                }
            }
            else
            {
                using ImageDescriptionGenerator generator = await ImageDescriptionGenerator.CreateAsync().AsTask(ct).ConfigureAwait(false);
                var description = await generator.DescribeAsync(image, ImageDescriptionKind.BriefDescription, new ContentFilterOptions()).AsTask(ct).ConfigureAwait(false);
                if (description.Status is ImageDescriptionResultStatus.BlockedByPolicy or ImageDescriptionResultStatus.ImageBlockedByContentModeration or
                    ImageDescriptionResultStatus.TextInImageBlockedByContentModeration or ImageDescriptionResultStatus.DescriptionTextBlockedByContentModeration)
                    return AnalyzerOutcomeKind.ContentRejected;
                if (description.Status != ImageDescriptionResultStatus.Complete || description.Description == null) return AnalyzerOutcomeKind.Failed;
                if (!string.IsNullOrWhiteSpace(description.Description)) descriptions.Add(new(description.Description, timestamp));
            }
        }
        return AnalyzerOutcomeKind.Succeeded;
    }

    private static OcrEngine? CreateLegacy(string? language) => string.IsNullOrWhiteSpace(language)
        ? OcrEngine.TryCreateFromUserProfileLanguages() : OcrEngine.TryCreateFromLanguage(new Language(language));
    private static NormalizedBounds Bounds(double left, double top, double right, double bottom, SoftwareBitmap bitmap)
    {
        double x = Math.Clamp(left / bitmap.PixelWidth, 0, 1), y = Math.Clamp(top / bitmap.PixelHeight, 0, 1);
        return new(x, y, Math.Clamp(right / bitmap.PixelWidth - x, 0, 1 - x), Math.Clamp(bottom / bitmap.PixelHeight - y, 0, 1 - y));
    }
    private static AnalyzerAvailability MapReady(AIFeatureReadyState ready) => ready switch
    {
        AIFeatureReadyState.Ready => AnalyzerAvailability.Ready,
        AIFeatureReadyState.NotReady => AnalyzerAvailability.PreparationRequired,
        AIFeatureReadyState.NotSupportedOnCurrentSystem or AIFeatureReadyState.DisabledByUser => AnalyzerAvailability.Unsupported,
        _ => AnalyzerAvailability.TemporarilyUnavailable,
    };
}
