namespace CaptureTool.Infrastructure.Abstractions.Cancellation;

public interface ICancellationService
{
    event EventHandler? CancelAllRequested;
    void CancelAll();
    Task CancelAllAsync();
    CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null);
    void Reset();
}