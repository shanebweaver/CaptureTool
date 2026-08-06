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
    private bool _isDisposed;

    public OperationLease Start(ImageEditOperation operation)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        Cancel(operation);

        var lease = new OperationLease(this, operation);
        _activeOperations.Add(operation, lease);
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
        foreach (OperationLease lease in _activeOperations.Values)
        {
            lease.CancelFromOwner();
        }

        _activeOperations.Clear();
    }

    private bool IsCurrent(OperationLease lease) =>
        !_isDisposed &&
        _activeOperations.TryGetValue(lease.Operation, out OperationLease? activeLease) &&
        ReferenceEquals(activeLease, lease);

    private void Complete(OperationLease lease)
    {
        if (IsCurrent(lease))
        {
            _activeOperations.Remove(lease.Operation);
        }
    }

    internal sealed class OperationLease : IDisposable
    {
        private readonly ImageEditOperationCoordinator _owner;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private bool _isDisposed;

        internal OperationLease(ImageEditOperationCoordinator owner, ImageEditOperation operation)
        {
            _owner = owner;
            Operation = operation;
            Token = _cancellationTokenSource.Token;
        }

        internal ImageEditOperation Operation { get; }

        public CancellationToken Token { get; }

        public bool IsCurrent => !_isDisposed && _owner.IsCurrent(this);

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _owner.Complete(this);
            _isDisposed = true;
            _cancellationTokenSource.Dispose();
        }

        internal void CancelFromOwner()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            try
            {
                _cancellationTokenSource.Cancel();
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
