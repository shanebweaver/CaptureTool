namespace CaptureTool.Application.Abstractions.Analysis;

/// <summary>
/// Supplied by the application consent workflow. The revision is durable across restart and changes on disable/revocation.
/// A lease serializes short admission/publication operations with policy changes; never hold it during inference.
/// Application composition denies analysis until the protected consent policy has loaded and permits it.
/// </summary>
public interface IAnalysisAuthorization
{
    ValueTask<IAnalysisAuthorizationLease> AcquireAsync(CancellationToken cancellationToken);
}

public interface IAnalysisAuthorizationLease : IDisposable
{
    bool IsAllowed { get; }
    Guid Revision { get; }
    CancellationToken Revoked { get; }
}
