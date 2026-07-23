namespace CaptureTool.Application.Abstractions.Edit.Image.ForegroundExtraction;

public interface IImageForegroundExtractionService
{
    ForegroundExtractionReadyState GetReadyState();

    Task<ForegroundExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<ForegroundExtractionResult> ExtractAsync(
        ForegroundExtractionRequest request,
        CancellationToken cancellationToken = default);
}
