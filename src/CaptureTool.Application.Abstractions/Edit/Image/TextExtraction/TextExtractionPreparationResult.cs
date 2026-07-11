namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public sealed record TextExtractionPreparationResult(
    TextExtractionPreparationStatus Status,
    string? ErrorMessage = null)
{
    public static TextExtractionPreparationResult Success { get; } = new(TextExtractionPreparationStatus.Success);

    public static TextExtractionPreparationResult Cancelled { get; } = new(TextExtractionPreparationStatus.Cancelled);

    public static TextExtractionPreparationResult NotSupported { get; } = new(TextExtractionPreparationStatus.NotSupported);

    public static TextExtractionPreparationResult Failed(string? errorMessage = null)
    {
        return new(TextExtractionPreparationStatus.Failed, errorMessage);
    }
}

