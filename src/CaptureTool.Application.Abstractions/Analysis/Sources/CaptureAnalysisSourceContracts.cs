using CaptureTool.Application.Abstractions.Analysis.Analyzers;
using CaptureTool.Application.Abstractions.Analysis.Policy;
using CaptureTool.Domain.Analysis;

namespace CaptureTool.Application.Abstractions.Analysis.Sources;

public sealed record CaptureAnalysisSourceVerificationRequest
{
    public CaptureAnalysisSourceVerificationRequest(
        CaptureAnalysisAuthorizationDecision authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (!authorization.IsAuthorized ||
            authorization.Request.Stage != CaptureAnalysisAuthorizationStage.SourceVerification)
        {
            throw new ArgumentException(
                "Source verification requires an authorized source-verification decision.",
                nameof(authorization));
        }

        Authorization = authorization;
    }

    public CaptureAnalysisAuthorizationDecision Authorization { get; }
}

public interface IVerifiedCaptureAnalysisSource :
    ICaptureAnalysisSource,
    IAsyncDisposable
{
    long CaptureSourceGeneration { get; }

    ProvisionalSourceStamp SourceStamp { get; }
}

public interface ICaptureAnalysisSourceVerifier
{
    ValueTask<IVerifiedCaptureAnalysisSource?> TryOpenVerifiedAsync(
        CaptureAnalysisSourceVerificationRequest request,
        CancellationToken cancellationToken = default);
}
