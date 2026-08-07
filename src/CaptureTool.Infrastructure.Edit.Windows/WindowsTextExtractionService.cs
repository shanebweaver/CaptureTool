using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
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
        cancellationToken.ThrowIfCancellationRequested();

        OcrEngine? engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
        {
            return TextExtractionResult.NotReady;
        }

        try
        {
            using SoftwareBitmap sourceBitmap = await LoadSoftwareBitmapAsync(request.SourceImage);

            cancellationToken.ThrowIfCancellationRequested();

            OcrResult ocrResult = await engine.RecognizeAsync(sourceBitmap);
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<RecognizedQrCodeRegion> qrCodes = QrCodeDetector.Detect(sourceBitmap);
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
        catch (OperationCanceledException)
        {
            return TextExtractionResult.Cancelled;
        }
        catch (Exception ex)
        {
            return TextExtractionResult.Failed(ex.Message);
        }
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

