namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

public interface ITextExtractionService
{
    TextExtractionReadyState GetReadyState();

    Task<TextExtractionPreparationResult> EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default);
}

