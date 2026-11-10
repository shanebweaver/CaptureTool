namespace CaptureTool.Presentation.Features.ImageEdit;

internal enum ImageEditOperation
{
    SuperResolution,
    TextExtraction,
    ImageDescription,
    ForegroundExtraction,
    ObjectErase,
    ObjectExtraction,
}

internal sealed class ImageEditOperationCoordinator : IDisposable
{
    private readonly Dictionary<ImageEditOperation, OperationLease> _activeOperations = [];
    private readonly HashSet<OperationLease> _runningOperations = [];
    private bool _isDisposed;

    public OperationLease Start(ImageEditOperation operation, CancellationToken revoked = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Cancel(operation);

        var lease = new OperationLease(this, operation, revoked);
        _activeOperations.Add(operation, lease);
        _runningOperations.Add(lease);
        return lease;
    }

    public void Cancel(ImageEditOperation operation)
    {
        if (_activeOperations.Remove(operation, out OperationLease? lease))
        {
            lease.CancelFromOwner();
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        List<Exception>? failures = null;
        foreach (OperationLease lease in _activeOperations.Values.ToArray())
        {
            try
            {
                lease.CancelFromOwner();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        _activeOperations.Clear();
        if (failures is not null)
        {
            throw new AggregateException("Failed to cancel editor operations.", failures);
        }
    }

    public async Task StopAsync()
    {
        // Superseded operations can still be unwinding after cancellation. Keep
        // them until the caller leaves its using scope, including its finally.
        try
        {
            Dispose();
        }
        finally
        {
            // A failing cancellation callback must not release the dispatcher
            // while this or another provider still owns native resources.
            await Task.WhenAll(_runningOperations.Select(lease => lease.Completion));
        }
    }

    private bool IsCurrent(OperationLease lease) =>
        !_isDisposed &&
        _activeOperations.TryGetValue(lease.Operation, out OperationLease? activeLease) &&
        ReferenceEquals(activeLease, lease);

    private void Complete(OperationLease lease)
    {
        _runningOperations.Remove(lease);
        if (IsCurrent(lease))
        {
            _activeOperations.Remove(lease.Operation);
        }
    }

    internal sealed class OperationLease : IDisposable
    {
        private readonly ImageEditOperationCoordinator _owner;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _isDisposed;
        private bool _isCanceled;
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal OperationLease(ImageEditOperationCoordinator owner, ImageEditOperation operation, CancellationToken revoked)
        {
            _owner = owner;
            Operation = operation;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(revoked);
            Token = _cancellationTokenSource.Token;
        }

        internal ImageEditOperation Operation { get; }

        public CancellationToken Token { get; }

        internal Task Completion => _completion.Task;

        public bool IsCurrent => !_isDisposed && !_isCanceled && _owner.IsCurrent(this);

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _owner.Complete(this);
            _isDisposed = true;
            _cancellationTokenSource.Dispose();
            _completion.TrySetResult();
        }

        internal void CancelFromOwner()
        {
            if (_isDisposed || _isCanceled)
            {
                return;
            }

            _isCanceled = true;
            _cancellationTokenSource.Cancel();
        }
    }
}
