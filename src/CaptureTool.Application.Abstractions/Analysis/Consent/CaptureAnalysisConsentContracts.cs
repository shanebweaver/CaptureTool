using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Consent;

public sealed record CaptureAnalysisConsentDisclosure
{
    public CaptureAnalysisConsentDisclosure(
        AnalysisPurpose purpose,
        AnalysisProcessingPolicy processingPolicy,
        IEnumerable<CapabilityDefinition> capabilities)
        : this(new CaptureAnalysisAuthorizationScope(purpose, processingPolicy, capabilities))
    {
    }

    public CaptureAnalysisConsentDisclosure(CaptureAnalysisAuthorizationScope authorizationScope)
    {
        ArgumentNullException.ThrowIfNull(authorizationScope);
        AuthorizationScope = authorizationScope;
    }

    public CaptureAnalysisAuthorizationScope AuthorizationScope { get; }

    public AnalysisPurpose Purpose => AuthorizationScope.Purpose;

    public AnalysisProcessingPolicy ProcessingPolicy => AuthorizationScope.ProcessingPolicy;

    public IReadOnlyList<CapabilityDefinition> Capabilities => AuthorizationScope.Capabilities;

    public bool IsEquivalentTo(CaptureAnalysisConsentDisclosure? other)
    {
        return AuthorizationScope.IsEquivalentTo(other?.AuthorizationScope);
    }
}

public enum CaptureAnalysisConsentDecision
{
    Unknown,
    Cancelled,
    Declined,
    GrantedForFutureCaptures,
}

public sealed record CaptureAnalysisConsentResponse
{
    public CaptureAnalysisConsentResponse(
        CaptureAnalysisConsentDisclosure disclosure,
        CaptureAnalysisConsentDecision decision)
    {
        ArgumentNullException.ThrowIfNull(disclosure);
        if (!Enum.IsDefined(decision) || decision == CaptureAnalysisConsentDecision.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        Disclosure = disclosure;
        Decision = decision;
    }

    // The response carries the exact disclosure shown to the user so a purpose or capability
    // change cannot be upgraded through a bare boolean grant.
    public CaptureAnalysisConsentDisclosure Disclosure { get; }

    public CaptureAnalysisConsentDecision Decision { get; }
}

public interface ICaptureAnalysisConsentDialogService
{
    ValueTask<CaptureAnalysisConsentResponse> RequestConsentAsync(
        CaptureAnalysisConsentDisclosure disclosure,
        CancellationToken cancellationToken = default);
}

public enum CaptureAnalysisSettingsAction
{
    Unknown,
    AuthorizeExistingCaptureBackfill,
    StopAnalyzingNewCaptures,
    TurnOffAndErase,
    ClearMemory,
    RebuildSearchIndex,
    ReanalyzeCaptures,
    RemoveFromMemory,
    DeleteCapture,
}

public enum CaptureAnalysisConfirmationDecision
{
    Unknown,
    Cancelled,
    Confirmed,
}

public readonly record struct CaptureAnalysisSettingsConfirmationRequest
{
    public CaptureAnalysisSettingsConfirmationRequest(CaptureAnalysisSettingsAction action)
    {
        if (!Enum.IsDefined(action) || action == CaptureAnalysisSettingsAction.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Action = action;
    }

    public CaptureAnalysisSettingsAction Action { get; }
}

public interface ICaptureAnalysisSettingsConfirmationDialogService
{
    ValueTask<CaptureAnalysisConfirmationDecision> ConfirmAsync(
        CaptureAnalysisSettingsConfirmationRequest request,
        CancellationToken cancellationToken = default);
}
