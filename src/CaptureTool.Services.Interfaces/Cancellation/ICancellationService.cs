namespace CaptureTool.Services.Interfaces.Cancellation;

public interface ICancellationService
{
    void CancelAll();
    Task CancelAllAsync();
    CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null);
    void Reset();
}