namespace CaptureTool.Services.Interfaces.Cancellation;

public interface ICancellationService
{
    event EventHandler? CancelAllRequested;
    void CancelAll();
    Task CancelAllAsync();
    CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null);
    void Reset();
}