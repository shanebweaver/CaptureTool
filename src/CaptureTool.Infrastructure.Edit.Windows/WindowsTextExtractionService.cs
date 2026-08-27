using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using System.Drawing;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WinRect = global::Windows.Foundation.Rect;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class WindowsTextExtractionService : ITextExtractionService
{
    public TextExtractionReadyState GetReadyState()
    {
        try
        {
            return GetCombinedReadyState(
                TextRecognizer.GetReadyState(),
                IsLegacyOcrAvailable());
        }
        catch
        {
            // The Windows AI runtime can be absent even when the app package contains its projections.
            return IsLegacyOcrAvailable()
                ? TextExtractionReadyState.Ready
                : TextExtractionReadyState.Unknown;
        }
    }

    public async Task<TextExtractionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            AIFeatureReadyState readyState = TextRecognizer.GetReadyState();
            if (readyState == AIFeatureReadyState.Ready)
            {
                return TextExtractionPreparationResult.Success;
            }

            if (readyState == AIFeatureReadyState.NotReady)
            {
                AIFeatureReadyResult result = await TextRecognizer
                    .EnsureReadyAsync()
                    .AsTask(cancellationToken);
                if (result.Status == AIFeatureReadyResultState.Success || IsLegacyOcrAvailable())
                {
                    return TextExtractionPreparationResult.Success;
                }

                return TextExtractionPreparationResult.Failed(GetErrorMessage(result));
            }

            return IsLegacyOcrAvailable()
                ? TextExtractionPreparationResult.Success
                : TextExtractionPreparationResult.NotSupported;
        }
        catch (OperationCanceledException)
        {
            return TextExtractionPreparationResult.Cancelled;
        }
        catch (Exception ex)
        {
            return IsLegacyOcrAvailable()
                ? TextExtractionPreparationResult.Success
                : TextExtractionPreparationResult.Failed(ex.Message);
        }
    }

    public async Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(request.SourceImage);

            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<RecognizedQrCodeRegion> qrCodes = QrCodeDetector.Detect(sourceBitmap);
            cancellationToken.ThrowIfCancellationRequested();

            TextExtractionResult? aiResult = await TryExtractWithWindowsAiAsync(
                sourceBitmap,
                request,
                qrCodes,
                cancellationToken);
            if (aiResult is not null)
            {
                return aiResult;
            }

            return await ExtractWithLegacyOcrAsync(
                sourceBitmap,
                request,
                qrCodes,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return TextExtractionResult.Cancelled;
        }
        catch (Exception ex)
        {
            return TextExtractionResult.Failed(ex.Message);
        }
    }

    internal static TextExtractionReadyState GetCombinedReadyState(
        AIFeatureReadyState aiReadyState,
        bool isLegacyOcrAvailable)
    {
        return aiReadyState switch
        {
            AIFeatureReadyState.Ready => TextExtractionReadyState.Ready,
            AIFeatureReadyState.NotReady => TextExtractionReadyState.PreparationNeeded,
            AIFeatureReadyState.NotSupportedOnCurrentSystem when isLegacyOcrAvailable => TextExtractionReadyState.Ready,
            AIFeatureReadyState.DisabledByUser when isLegacyOcrAvailable => TextExtractionReadyState.Ready,
            AIFeatureReadyState.NotSupportedOnCurrentSystem => TextExtractionReadyState.NotSupported,
            AIFeatureReadyState.DisabledByUser => TextExtractionReadyState.Disabled,
            _ when isLegacyOcrAvailable => TextExtractionReadyState.Ready,
            _ => TextExtractionReadyState.Unknown
        };
    }

    internal static RectangleF ToRectangleF(RecognizedTextBoundingBox bounds)
    {
        float left = (float)Math.Min(
            Math.Min(bounds.TopLeft.X, bounds.TopRight.X),
            Math.Min(bounds.BottomLeft.X, bounds.BottomRight.X));
        float top = (float)Math.Min(
            Math.Min(bounds.TopLeft.Y, bounds.TopRight.Y),
            Math.Min(bounds.BottomLeft.Y, bounds.BottomRight.Y));
        float right = (float)Math.Max(
            Math.Max(bounds.TopLeft.X, bounds.TopRight.X),
            Math.Max(bounds.BottomLeft.X, bounds.BottomRight.X));
        float bottom = (float)Math.Max(
            Math.Max(bounds.TopLeft.Y, bounds.TopRight.Y),
            Math.Max(bounds.BottomLeft.Y, bounds.BottomRight.Y));

        return RectangleF.FromLTRB(left, top, right, bottom);
    }

    private static async Task<TextExtractionResult?> TryExtractWithWindowsAiAsync(
        SoftwareBitmap sourceBitmap,
        TextExtractionRequest request,
        IReadOnlyList<RecognizedQrCodeRegion> qrCodes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TextRecognizer.GetReadyState() != AIFeatureReadyState.Ready)
            {
                return null;
            }

            using ImageBuffer imageBuffer = ImageBuffer.CreateForSoftwareBitmap(sourceBitmap);
            using TextRecognizer recognizer = await TextRecognizer
                .CreateAsync()
                .AsTask(cancellationToken);
            Microsoft.Windows.AI.Imaging.RecognizedText recognizedText = await recognizer
                .RecognizeTextFromImageAsync(imageBuffer)
                .AsTask(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            List<RecognizedTextRegion> regions = [];
            List<string> recognizedLines = [];
            int lineIndex = 0;
            foreach (RecognizedLine line in recognizedText.Lines)
            {
                List<string> recognizedWords = [];
                int wordIndex = 0;
                foreach (RecognizedWord word in line.Words)
                {
                    RectangleF bounds = ToRectangleF(word.BoundingBox);
                    if (!string.IsNullOrWhiteSpace(word.Text) &&
                        bounds.Width > 0 &&
                        bounds.Height > 0 &&
                        !QrCodeDetector.ShouldExcludeText(bounds, qrCodes))
                    {
                        regions.Add(new RecognizedTextRegion(word.Text, bounds, lineIndex, wordIndex));
                        recognizedWords.Add(word.Text);
                    }

                    wordIndex++;
                }

                if (recognizedWords.Count > 0)
                {
                    recognizedLines.Add(string.Join(' ', recognizedWords));
                }

                lineIndex++;
            }

            string documentText = CombineRecognizedValues(
                string.Join(Environment.NewLine, recognizedLines),
                qrCodes);
            return TextExtractionResult.Success(new RecognizedTextDocument(
                documentText,
                request.SourceSize,
                regions,
                qrCodes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Initialization and recognition failures fall back to the broadly supported Windows OCR engine.
            return null;
        }
    }

    private static async Task<TextExtractionResult> ExtractWithLegacyOcrAsync(
        SoftwareBitmap sourceBitmap,
        TextExtractionRequest request,
        IReadOnlyList<RecognizedQrCodeRegion> qrCodes,
        CancellationToken cancellationToken)
    {
        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return TextExtractionResult.NotReady;
        }

        OcrResult ocrResult = await engine.RecognizeAsync(sourceBitmap);
        cancellationToken.ThrowIfCancellationRequested();

        List<RecognizedTextRegion> regions = [];
        List<string> recognizedLines = [];
        for (int lineIndex = 0; lineIndex < ocrResult.Lines.Count; lineIndex++)
        {
            OcrLine line = ocrResult.Lines[lineIndex];
            List<string> recognizedWords = [];
            for (int wordIndex = 0; wordIndex < line.Words.Count; wordIndex++)
            {
                OcrWord word = line.Words[wordIndex];
                RectangleF bounds = ToRectangleF(word.BoundingRect);
                if (!string.IsNullOrWhiteSpace(word.Text) &&
                    bounds.Width > 0 &&
                    bounds.Height > 0 &&
                    !QrCodeDetector.ShouldExcludeText(bounds, qrCodes))
                {
                    regions.Add(new RecognizedTextRegion(word.Text, bounds, lineIndex, wordIndex));
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
            qrCodes);
        return TextExtractionResult.Success(new RecognizedTextDocument(
            documentText,
            request.SourceSize,
            regions,
            qrCodes));
    }

    private static async Task<SoftwareBitmap> LoadSoftwareBitmapAsync(Stream sourceStream)
    {
        if (sourceStream.CanSeek)
        {
            sourceStream.Position = 0;
        }

        using IRandomAccessStream randomAccessStream = sourceStream.AsRandomAccessStream();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    private static RectangleF ToRectangleF(WinRect rect)
    {
        return new(
            (float)rect.X,
            (float)rect.Y,
            (float)rect.Width,
            (float)rect.Height);
    }

    private static bool IsLegacyOcrAvailable()
    {
        try
        {
            return OcrEngine.TryCreateFromUserProfileLanguages() is not null;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetErrorMessage(AIFeatureReadyResult result)
    {
        return !string.IsNullOrWhiteSpace(result.ErrorDisplayText)
            ? result.ErrorDisplayText
            : result.ExtendedError.Message;
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
}

