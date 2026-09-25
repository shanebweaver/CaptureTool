using CaptureTool.Application.Abstractions.Ai;

namespace CaptureTool.Application.Abstractions.Edit.Image.TextExtraction;

/// <summary>Consent for the standalone editor tool; independent of capture analysis.</summary>
public interface ITextExtractionConsentService
{
    AiFeatureConsentState State { get; }
    CancellationToken Revoked { get; }
    Task<bool> EnsureConsentAsync(CancellationToken cancellationToken = default);
}

public interface ITextExtractionConsentPrompt
{
    Task<bool> ConfirmAsync(CancellationToken cancellationToken);
}
