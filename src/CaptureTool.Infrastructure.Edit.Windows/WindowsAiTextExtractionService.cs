using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsAiTextExtractionService :
    ITextExtractionService,
    ITextExtractionAnalysisService
{
    private static readonly string? RuntimeVersion =
        typeof(TextRecognizer).Assembly.GetName().Version?.ToString();
    private static readonly TextExtractionModelDescriptor Descriptor = new(
        ProducerId: "microsoft-windows",
        ModelId: "windows-app-sdk-text-recognizer",
        ModelVersion: null,
        RuntimeId: "windows-app-sdk-ai",
        RuntimeVersion);

    public TextExtractionModelDescriptor ModelDescriptor => Descriptor;

    public TextExtractionReadyState GetReadyState()
    {
        try
        {
            return MapReadyState(TextRecognizer.GetReadyState());
        }
        catch
        {
            return TextExtractionReadyState.Unknown;
        }
    }

    public async Task<TextExtractionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TextExtractionReadyState state = GetReadyState();
        if (state == TextExtractionReadyState.Ready)
        {
            return TextExtractionPreparationResult.Success;
        }

        if (state is TextExtractionReadyState.NotSupported or TextExtractionReadyState.Disabled)
        {
            return TextExtractionPreparationResult.NotSupported;
        }

        try
        {
            AIFeatureReadyResult result = await TextRecognizer.EnsureReadyAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            return result.Status == AIFeatureReadyResultState.Success
                ? TextExtractionPreparationResult.Success
                : TextExtractionPreparationResult.Failed(GetErrorMessage(result));
        }
        catch (OperationCanceledException)
        {
            return TextExtractionPreparationResult.Cancelled;
        }
        catch (COMException exception)
        {
            return TextExtractionPreparationResult.Failed(exception.Message);
        }
        catch (Exception exception)
        {
            return TextExtractionPreparationResult.Failed(exception.Message);
        }
    }

    public async Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        WindowsAiTextExtractionAttempt attempt = await RecognizeAsync(
            request.SourceImage,
            detectQrCodes: true,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result.Status switch
        {
            TextExtractionAnalysisStatus.Succeeded => CreateInteractiveResult(
                attempt,
                request.SourceSize),
            TextExtractionAnalysisStatus.Unavailable => TextExtractionResult.NotReady,
            TextExtractionAnalysisStatus.Cancelled => TextExtractionResult.Cancelled,
            _ => TextExtractionResult.Failed(attempt.ErrorMessage),
        };
    }

    public async Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
        Stream sourceImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceImage);
        cancellationToken.ThrowIfCancellationRequested();
        WindowsAiTextExtractionAttempt attempt = await RecognizeAsync(
            sourceImage,
            detectQrCodes: false,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result;
    }

    internal static TextExtractionReadyState MapReadyState(AIFeatureReadyState readyState)
    {
        return readyState switch
        {
            AIFeatureReadyState.Ready => TextExtractionReadyState.Ready,
            AIFeatureReadyState.NotReady => TextExtractionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem or
            AIFeatureReadyState.NotCompatibleWithSystemHardware or
            AIFeatureReadyState.CapabilityMissing or
            AIFeatureReadyState.OSUpdateNeeded => TextExtractionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => TextExtractionReadyState.Disabled,
            _ => TextExtractionReadyState.Unknown,
        };
    }

    private static async Task<WindowsAiTextExtractionAttempt> RecognizeAsync(
        Stream sourceImage,
        bool detectQrCodes,
        CancellationToken cancellationToken)
    {
        if (MapReadyState(TextRecognizer.GetReadyState()) != TextExtractionReadyState.Ready)
        {
            return WindowsAiTextExtractionAttempt.Unavailable;
        }

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(sourceImage)
                .ConfigureAwait(false);
            using ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(sourceBitmap);
            using TextRecognizer recognizer = await TextRecognizer.CreateAsync()
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            RecognizedText recognized = await recognizer
                .RecognizeTextFromImageAsync(imageBuffer)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            TextExtractionAnalysisDocument document = CreateAnalysisDocument(
                recognized,
                sourceBitmap);
            IReadOnlyList<RecognizedQrCodeRegion> qrCodes = detectQrCodes
                ? QrCodeDetector.Detect(sourceBitmap)
                : [];
            return WindowsAiTextExtractionAttempt.Succeeded(document, qrCodes);
        }
        catch (OperationCanceledException)
        {
            return WindowsAiTextExtractionAttempt.Cancelled;
        }
        catch (COMException exception)
        {
            return WindowsAiTextExtractionAttempt.TransientFailure(exception.Message);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return WindowsAiTextExtractionAttempt.TerminalFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return WindowsAiTextExtractionAttempt.TransientFailure(exception.Message);
        }
    }

    private static TextExtractionAnalysisDocument CreateAnalysisDocument(
        RecognizedText recognized,
        SoftwareBitmap sourceBitmap)
    {
        var lines = new List<TextExtractionAnalysisLine>();
        for (int lineIndex = 0; lineIndex < recognized.Lines.Length; lineIndex++)
        {
            RecognizedLine line = recognized.Lines[lineIndex];
            var words = new List<TextExtractionAnalysisWord>();
            for (int wordIndex = 0; wordIndex < line.Words.Length; wordIndex++)
            {
                RecognizedWord word = line.Words[wordIndex];
                if (!string.IsNullOrWhiteSpace(word.Text) && TryToPixelBounds(
                    word.BoundingBox,
                    sourceBitmap.PixelWidth,
                    sourceBitmap.PixelHeight,
                    out TextExtractionPixelBounds bounds))
                {
                    words.Add(new TextExtractionAnalysisWord(
                        word.Text,
                        bounds,
                        wordIndex,
                        word.MatchConfidence));
                }
            }

            if (words.Count == 0)
            {
                continue;
            }

            TextExtractionPixelBounds lineBounds = TryToPixelBounds(
                line.BoundingBox,
                sourceBitmap.PixelWidth,
                sourceBitmap.PixelHeight,
                out TextExtractionPixelBounds recognizedBounds)
                    ? recognizedBounds
                    : Union(words.Select(word => word.Bounds));
            lines.Add(new TextExtractionAnalysisLine(
                string.IsNullOrWhiteSpace(line.Text)
                    ? string.Join(' ', words.Select(word => word.Text))
                    : line.Text,
                lineBounds,
                lineIndex,
                words));
        }

        IReadOnlyList<TextExtractionAnalysisRegion> regions = lines.Count == 0
            ? []
            : [new TextExtractionAnalysisRegion(
                Union(lines.Select(line => line.Bounds)),
                0,
                lines)];
        string fullText = string.Join(Environment.NewLine, lines.Select(line => line.Text));
        return new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight),
            fullText,
            [],
            regions);
    }

    private static bool TryToPixelBounds(
        RecognizedTextBoundingBox polygon,
        int rasterWidth,
        int rasterHeight,
        out TextExtractionPixelBounds bounds)
    {
        double[] xs =
        [
            polygon.TopLeft.X,
            polygon.TopRight.X,
            polygon.BottomRight.X,
            polygon.BottomLeft.X,
        ];
        double[] ys =
        [
            polygon.TopLeft.Y,
            polygon.TopRight.Y,
            polygon.BottomRight.Y,
            polygon.BottomLeft.Y,
        ];
        if (xs.Any(value => !double.IsFinite(value)) ||
            ys.Any(value => !double.IsFinite(value)))
        {
            bounds = default;
            return false;
        }

        double x = Math.Clamp(xs.Min(), 0, rasterWidth);
        double y = Math.Clamp(ys.Min(), 0, rasterHeight);
        double right = Math.Clamp(xs.Max(), 0, rasterWidth);
        double bottom = Math.Clamp(ys.Max(), 0, rasterHeight);
        if (right <= x || bottom <= y)
        {
            bounds = default;
            return false;
        }

        bounds = new TextExtractionPixelBounds(x, y, right - x, bottom - y);
        return true;
    }

    private static TextExtractionPixelBounds Union(IEnumerable<TextExtractionPixelBounds> values)
    {
        TextExtractionPixelBounds[] bounds = [.. values];
        double x = bounds.Min(value => value.X);
        double y = bounds.Min(value => value.Y);
        double right = bounds.Max(value => value.X + value.Width);
        double bottom = bounds.Max(value => value.Y + value.Height);
        return new TextExtractionPixelBounds(x, y, right - x, bottom - y);
    }

    private static TextExtractionResult CreateInteractiveResult(
        WindowsAiTextExtractionAttempt attempt,
        Size sourceSize)
    {
        TextExtractionAnalysisDocument? document = attempt.Result.Document;
        if (document == null)
        {
            return TextExtractionResult.Failed(attempt.ErrorMessage);
        }

        var regions = new List<RecognizedTextRegion>();
        var lines = new List<string>();
        foreach (TextExtractionAnalysisLine line in document.Regions.SelectMany(region => region.Lines))
        {
            var words = new List<string>();
            foreach (TextExtractionAnalysisWord word in line.Words)
            {
                var bounds = new RectangleF(
                    (float)word.Bounds.X,
                    (float)word.Bounds.Y,
                    (float)word.Bounds.Width,
                    (float)word.Bounds.Height);
                if (!QrCodeDetector.ShouldExcludeText(bounds, attempt.QrCodes))
                {
                    regions.Add(new RecognizedTextRegion(word.Text, bounds, line.Order, word.Order));
                    words.Add(word.Text);
                }
            }

            if (words.Count > 0)
            {
                lines.Add(string.Join(' ', words));
            }
        }

        IEnumerable<string> values = attempt.QrCodes.Select(code => code.Value);
        if (lines.Count > 0)
        {
            values = values.Prepend(string.Join(Environment.NewLine, lines));
        }

        return TextExtractionResult.Success(new RecognizedTextDocument(
            string.Join(Environment.NewLine, values),
            sourceSize,
            regions,
            attempt.QrCodes));
    }

    private static async Task<SoftwareBitmap> LoadSoftwareBitmapAsync(Stream sourceStream)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        using IRandomAccessStream randomAccessStream = sourceStream.AsRandomAccessStream();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.DoNotColorManage);
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
    }

    private sealed record WindowsAiTextExtractionAttempt(
        TextExtractionAnalysisResult Result,
        IReadOnlyList<RecognizedQrCodeRegion> QrCodes,
        string? ErrorMessage)
    {
        public static WindowsAiTextExtractionAttempt Succeeded(
            TextExtractionAnalysisDocument document,
            IReadOnlyList<RecognizedQrCodeRegion> qrCodes) =>
            new(TextExtractionAnalysisResult.Succeeded(document), qrCodes, null);

        public static WindowsAiTextExtractionAttempt Unavailable { get; } = new(
            TextExtractionAnalysisResult.Unavailable, [], null);

        public static WindowsAiTextExtractionAttempt Cancelled { get; } = new(
            TextExtractionAnalysisResult.Cancelled, [], null);

        public static WindowsAiTextExtractionAttempt TransientFailure(string? message) =>
            new(TextExtractionAnalysisResult.TransientFailure, [], message);

        public static WindowsAiTextExtractionAttempt TerminalFailure(string? message) =>
            new(TextExtractionAnalysisResult.TerminalFailure, [], message);
    }
}
