using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WinRect = global::Windows.Foundation.Rect;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsTextExtractionService :
    ITextExtractionService,
    ITextExtractionAnalysisService
{
    private static readonly TextExtractionModelDescriptor Descriptor = new(
        ProducerId: "microsoft-windows",
        ModelId: "windows-media-ocr",
        ModelVersion: null,
        RuntimeId: "windows-media-ocr",
        RuntimeVersion: null);

    public TextExtractionModelDescriptor ModelDescriptor => Descriptor;

    public TextExtractionReadyState GetReadyState()
    {
        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages() is null
                ? TextExtractionReadyState.NotSupported
                : TextExtractionReadyState.Ready;
        }
        catch
        {
            return TextExtractionReadyState.Unknown;
        }
    }

    public Task<TextExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(GetReadyState() == TextExtractionReadyState.Ready
            ? TextExtractionPreparationResult.Success
            : TextExtractionPreparationResult.NotSupported);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        WindowsTextExtractionAttempt attempt = await RecognizeAsync(
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
            TextExtractionAnalysisStatus.TransientFailure or
                TextExtractionAnalysisStatus.TerminalFailure =>
                    TextExtractionResult.Failed(attempt.ErrorMessage),
            _ => TextExtractionResult.Failed(attempt.ErrorMessage),
        };
    }

    public async Task<TextExtractionAnalysisResult> ExtractAnalysisAsync(
        Stream sourceImage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceImage);
        cancellationToken.ThrowIfCancellationRequested();

        WindowsTextExtractionAttempt attempt = await RecognizeAsync(
            sourceImage,
            detectQrCodes: false,
            cancellationToken).ConfigureAwait(false);
        return attempt.Result;
    }

    private static async Task<WindowsTextExtractionAttempt> RecognizeAsync(
        Stream sourceImage,
        bool detectQrCodes,
        CancellationToken cancellationToken)
    {
        OcrEngine? engine;
        try
        {
            engine = OcrEngine.TryCreateFromUserProfileLanguages();
        }
        catch (COMException exception)
        {
            return WindowsTextExtractionAttempt.TransientFailure(exception.Message);
        }

        if (engine is null)
        {
            return WindowsTextExtractionAttempt.Unavailable;
        }

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(sourceImage)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            OcrResult ocrResult = await engine.RecognizeAsync(sourceBitmap);
            cancellationToken.ThrowIfCancellationRequested();

            TextExtractionAnalysisDocument document = CreateAnalysisDocument(
                engine,
                ocrResult,
                sourceBitmap);
            IReadOnlyList<RecognizedQrCodeRegion> qrCodes = detectQrCodes
                ? QrCodeDetector.Detect(sourceBitmap)
                : [];
            cancellationToken.ThrowIfCancellationRequested();

            return WindowsTextExtractionAttempt.Succeeded(document, qrCodes);
        }
        catch (OperationCanceledException)
        {
            return WindowsTextExtractionAttempt.Cancelled;
        }
        catch (COMException exception)
        {
            return WindowsTextExtractionAttempt.TransientFailure(exception.Message);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or ArgumentException)
        {
            return WindowsTextExtractionAttempt.TerminalFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return WindowsTextExtractionAttempt.TerminalFailure(exception.Message);
        }
    }

    private static TextExtractionResult CreateInteractiveResult(
        WindowsTextExtractionAttempt attempt,
        Size sourceSize)
    {
        TextExtractionAnalysisDocument? document = attempt.Result.Document;
        if (document is null)
        {
            return TextExtractionResult.Failed(attempt.ErrorMessage);
        }

        List<RecognizedTextRegion> regions = [];
        List<string> recognizedLines = [];
        foreach (TextExtractionAnalysisLine line in document.Regions.SelectMany(region => region.Lines))
        {
            List<string> recognizedWords = [];
            foreach (TextExtractionAnalysisWord word in line.Words)
            {
                RectangleF bounds = ToRectangleF(word.Bounds);
                if (!string.IsNullOrWhiteSpace(word.Text) &&
                    bounds.Width > 0 &&
                    bounds.Height > 0 &&
                    !QrCodeDetector.ShouldExcludeText(bounds, attempt.QrCodes))
                {
                    regions.Add(new RecognizedTextRegion(
                        word.Text,
                        bounds,
                        line.Order,
                        word.Order));
                    recognizedWords.Add(word.Text);
                }
            }

            if (recognizedWords.Count > 0)
            {
                recognizedLines.Add(string.Join(' ', recognizedWords));
            }
        }

        string documentText = CombineRecognizedValues(
            string.Join(Environment.NewLine, recognizedLines),
            attempt.QrCodes);
        return TextExtractionResult.Success(new RecognizedTextDocument(
            documentText,
            sourceSize,
            regions,
            attempt.QrCodes));
    }

    private static TextExtractionAnalysisDocument CreateAnalysisDocument(
        OcrEngine engine,
        OcrResult ocrResult,
        SoftwareBitmap sourceBitmap)
    {
        List<TextExtractionAnalysisLine> lines = [];
        for (int lineIndex = 0; lineIndex < ocrResult.Lines.Count; lineIndex++)
        {
            OcrLine line = ocrResult.Lines[lineIndex];
            List<TextExtractionAnalysisWord> words = [];
            for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
            {
                OcrWord word = line.Words[wordIndex];
                if (!string.IsNullOrWhiteSpace(word.Text) &&
                    TryToPixelBounds(
                        word.BoundingRect,
                        sourceBitmap.PixelWidth,
                        sourceBitmap.PixelHeight,
                        out TextExtractionPixelBounds bounds))
                {
                    words.Add(new TextExtractionAnalysisWord(
                        word.Text,
                        bounds,
                        wordIndex));
                }
            }

            if (words.Count == 0)
            {
                continue;
            }

            TextExtractionPixelBounds lineBounds = Union(words.Select(word => word.Bounds));
            string lineText = string.IsNullOrWhiteSpace(line.Text)
                ? string.Join(' ', words.Select(word => word.Text))
                : line.Text;
            lines.Add(new TextExtractionAnalysisLine(
                lineText,
                lineBounds,
                lineIndex,
                words));
        }

        IReadOnlyList<TextExtractionAnalysisRegion> regions = lines.Count == 0
            ? []
            : [new TextExtractionAnalysisRegion(
                Union(lines.Select(line => line.Bounds)),
                Order: 0,
                lines)];

        string? languageTag = engine.RecognizerLanguage?.LanguageTag;
        IReadOnlyList<TextExtractionLanguageCandidate> languages =
            string.IsNullOrWhiteSpace(languageTag)
                ? []
                : [new TextExtractionLanguageCandidate(languageTag, Order: 0)];

        return new TextExtractionAnalysisDocument(
            new TextExtractionRasterSize(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight),
            ocrResult.Text ?? string.Empty,
            languages,
            regions);
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

    private static bool TryToPixelBounds(
        WinRect rect,
        int rasterWidth,
        int rasterHeight,
        out TextExtractionPixelBounds bounds)
    {
        double right = rect.X + rect.Width;
        double bottom = rect.Y + rect.Height;
        if (!double.IsFinite(rect.X) ||
            !double.IsFinite(rect.Y) ||
            !double.IsFinite(right) ||
            !double.IsFinite(bottom))
        {
            bounds = default;
            return false;
        }

        double x = Math.Clamp(rect.X, 0, rasterWidth);
        double y = Math.Clamp(rect.Y, 0, rasterHeight);
        right = Math.Clamp(right, 0, rasterWidth);
        bottom = Math.Clamp(bottom, 0, rasterHeight);
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

    private static RectangleF ToRectangleF(TextExtractionPixelBounds bounds)
    {
        return new(
            (float)bounds.X,
            (float)bounds.Y,
            (float)bounds.Width,
            (float)bounds.Height);
    }

    private static string CombineRecognizedValues(
        string? recognizedText,
        IReadOnlyList<RecognizedQrCodeRegion> qrCodes)
    {
        IEnumerable<string> values = qrCodes.Select(qrCode => qrCode.Value);
        if (!string.IsNullOrWhiteSpace(recognizedText))
        {
            values = values.Prepend(recognizedText.TrimEnd());
        }

        return string.Join(Environment.NewLine, values);
    }

    private sealed record WindowsTextExtractionAttempt(
        TextExtractionAnalysisResult Result,
        IReadOnlyList<RecognizedQrCodeRegion> QrCodes,
        string? ErrorMessage)
    {
        public static WindowsTextExtractionAttempt Succeeded(
            TextExtractionAnalysisDocument document,
            IReadOnlyList<RecognizedQrCodeRegion> qrCodes)
        {
            return new(TextExtractionAnalysisResult.Succeeded(document), qrCodes, null);
        }

        public static WindowsTextExtractionAttempt Unavailable { get; } = new(
            TextExtractionAnalysisResult.Unavailable,
            [],
            null);

        public static WindowsTextExtractionAttempt Cancelled { get; } = new(
            TextExtractionAnalysisResult.Cancelled,
            [],
            null);

        public static WindowsTextExtractionAttempt TransientFailure(string? errorMessage)
        {
            return new(TextExtractionAnalysisResult.TransientFailure, [], errorMessage);
        }

        public static WindowsTextExtractionAttempt TerminalFailure(string? errorMessage)
        {
            return new(TextExtractionAnalysisResult.TerminalFailure, [], errorMessage);
        }
    }
}
