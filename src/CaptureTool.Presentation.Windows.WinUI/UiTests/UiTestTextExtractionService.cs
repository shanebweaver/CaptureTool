using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;
using System.Drawing;

namespace CaptureTool.Presentation.Windows.WinUI.UiTests;

internal sealed class UiTestTextExtractionService : ITextExtractionService
{
    private static readonly Size BaseImageSize = new(420, 220);

    public TextExtractionReadyState GetReadyState()
    {
        return TextExtractionReadyState.Ready;
    }

    public Task<TextExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(TextExtractionPreparationResult.Success);
    }

    public Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecognizedTextRegion[] regions = [
            new("OCR MODE", ScaleBounds(new RectangleF(48, 42, 236, 42), request.SourceSize)),
            new("SAMPLE TEXT", ScaleBounds(new RectangleF(48, 112, 306, 38), request.SourceSize))
        ];

        return Task.FromResult(TextExtractionResult.Success(new RecognizedTextDocument(
            "OCR MODE" + Environment.NewLine + "SAMPLE TEXT",
            request.SourceSize,
            regions)));
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
