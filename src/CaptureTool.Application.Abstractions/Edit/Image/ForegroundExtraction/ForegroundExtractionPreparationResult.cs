namespace CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;

public sealed record ForegroundExtractionPreparationResult(
    ForegroundExtractionPreparationStatus Status,
    string? ErrorMessage = null)
{
    public static ForegroundExtractionPreparationResult Success { get; } = new(ForegroundExtractionPreparationStatus.Success);

    public static ForegroundExtractionPreparationResult Cancelled { get; } = new(ForegroundExtractionPreparationStatus.Cancelled);

    public static ForegroundExtractionPreparationResult NotSupported { get; } = new(ForegroundExtractionPreparationStatus.NotSupported);

    public static ForegroundExtractionPreparationResult Failed(string? errorMessage = null)
    {
        return new(ForegroundExtractionPreparationStatus.Failed, errorMessage);
    }
}
