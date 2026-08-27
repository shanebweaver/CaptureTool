using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestTextExtractionService : ITextExtractionService
{
    private static readonly Size BaseImageSize = new(420, 220);
    private static readonly TimeSpan SimulatedInferenceDelay = TimeSpan.FromSeconds(2);

    public TextExtractionReadyState GetReadyState()
    {
        return TextExtractionReadyState.Ready;
    }

    public Task<TextExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TextExtractionPreparationResult.Success);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Delay(SimulatedInferenceDelay, cancellationToken);

        Size sourceSize = request.SourceSize.Width > 0 && request.SourceSize.Height > 0
            ? request.SourceSize
            : BaseImageSize;

        RecognizedTextRegion[] regions = [
            new("OCR", ScaleBounds(new RectangleF(48, 42, 106, 42), sourceSize), 0, 0),
            new("MODE", ScaleBounds(new RectangleF(166, 42, 118, 42), sourceSize), 0, 1),
            new("SAMPLE", ScaleBounds(new RectangleF(48, 112, 164, 38), sourceSize), 1, 0),
            new("TEXT", ScaleBounds(new RectangleF(224, 112, 130, 38), sourceSize), 1, 1)
        ];
        RecognizedQrCodeRegion[] qrCodes = [
            new(
                "https://example.com/capturetool",
                ScaleBounds(new RectangleF(262, 40, 125, 140), sourceSize))
        ];

        return TextExtractionResult.Success(new RecognizedTextDocument(
            "OCR MODE" + Environment.NewLine +
                "SAMPLE TEXT" + Environment.NewLine +
                "https://example.com/capturetool",
            sourceSize,
            regions,
            qrCodes));
    }

    private static RectangleF ScaleBounds(RectangleF bounds, Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0)
        {
            return bounds;
        }

        float scaleX = imageSize.Width / (float)BaseImageSize.Width;
        float scaleY = imageSize.Height / (float)BaseImageSize.Height;

        return new RectangleF(
            bounds.X * scaleX,
            bounds.Y * scaleY,
            bounds.Width * scaleX,
            bounds.Height * scaleY);
    }
}
