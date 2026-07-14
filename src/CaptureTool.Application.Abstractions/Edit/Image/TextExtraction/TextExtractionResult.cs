namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record TextExtractionResult(
    TextExtractionStatus Status,
    RecognizedTextDocument? Document = null,
    string? ErrorMessage = null)
{
    public static TextExtractionResult Success(RecognizedTextDocument document)
    {
        return new(TextExtractionStatus.Success, document);
    }

    public static TextExtractionResult Cancelled { get; } = new(TextExtractionStatus.Cancelled);

    public static TextExtractionResult NotSupported { get; } = new(TextExtractionStatus.NotSupported);

    public static TextExtractionResult NotReady { get; } = new(TextExtractionStatus.NotReady);

    public static TextExtractionResult TooLarge(string? errorMessage = null)
    {
        return new(TextExtractionStatus.TooLarge, ErrorMessage: errorMessage);
    }

    public static TextExtractionResult Failed(string? errorMessage = null)
    {
        return new(TextExtractionStatus.Failed, ErrorMessage: errorMessage);
    }
}

