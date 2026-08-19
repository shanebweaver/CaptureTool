using CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

namespace CaptureTool.Infrastructure.Edit.Windows;

public sealed class FallbackWindowsTextExtractionService : ITextExtractionService
{
    private readonly WindowsAiTextExtractionService _windowsAi;
    private readonly WindowsTextExtractionService _legacy;

    public FallbackWindowsTextExtractionService(
        WindowsAiTextExtractionService windowsAi,
        WindowsTextExtractionService legacy)
    {
        _windowsAi = windowsAi;
        _legacy = legacy;
    }

    public TextExtractionReadyState GetReadyState()
    {
        TextExtractionReadyState windowsAiState = _windowsAi.GetReadyState();
        return windowsAiState == TextExtractionReadyState.Ready
            ? windowsAiState
            : _legacy.GetReadyState();
    }

    public async Task<TextExtractionPreparationResult> EnsureReadyAsync(
        CancellationToken cancellationToken = default)
    {
        TextExtractionReadyState windowsAiState = _windowsAi.GetReadyState();
        if (windowsAiState == TextExtractionReadyState.Ready)
        {
            return TextExtractionPreparationResult.Success;
        }

        if (windowsAiState == TextExtractionReadyState.PreparationNeeded)
        {
            TextExtractionPreparationResult prepared = await _windowsAi
                .EnsureReadyAsync(cancellationToken)
                .ConfigureAwait(false);
            if (prepared.Status == TextExtractionPreparationStatus.Success)
            {
                return prepared;
            }

            if (prepared.Status == TextExtractionPreparationStatus.Cancelled)
            {
                return prepared;
            }
        }

        return await _legacy.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        TextExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_windowsAi.GetReadyState() == TextExtractionReadyState.Ready)
        {
            TextExtractionResult result = await _windowsAi
                .ExtractAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status is TextExtractionStatus.Success or TextExtractionStatus.Cancelled)
            {
                return result;
            }

            if (request.SourceImage.CanSeek)
            {
                request.SourceImage.Position = 0;
            }
        }

        return await _legacy.ExtractAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
