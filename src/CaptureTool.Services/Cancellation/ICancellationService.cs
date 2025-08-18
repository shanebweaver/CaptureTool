using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Services.Cancellation;

public interface ICancellationService
{
    void CancelAll();
    Task CancelAllAsync();
    CancellationTokenSource GetLinkedCancellationTokenSource(CancellationToken? cancellationToken = null);
    void Reset();
}