namespace CaptureTool.Presentation.Activation;

internal sealed class StartupActivationQueue<T>
{
    private readonly Lock _lock = new();
    private readonly Queue<T> _pending = [];
    private Action<T>? _consumer;
    private bool _isDraining;

    public void Enqueue(T activation)
    {
        bool shouldDrain;
        lock (_lock)
        {
            _pending.Enqueue(activation);
            shouldDrain = TryBeginDrain();
        }

        if (shouldDrain)
        {
            Drain();
        }
    }

    public void Attach(Action<T> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        bool shouldDrain;
        lock (_lock)
        {
            if (_consumer is not null)
            {
                throw new InvalidOperationException("An activation consumer is already attached.");
            }

            _consumer = consumer;
            shouldDrain = TryBeginDrain();
        }

        if (shouldDrain)
        {
            Drain();
        }
    }

    private bool TryBeginDrain()
    {
        if (_consumer is null || _isDraining || _pending.Count == 0)
        {
            return false;
        }

        _isDraining = true;
        return true;
    }

    private void Drain()
    {
        while (true)
        {
            T activation;
            Action<T> consumer;
            lock (_lock)
            {
                if (_pending.Count == 0)
                {
                    _isDraining = false;
                    return;
                }

                activation = _pending.Peek();
                consumer = _consumer!;
            }

            try
            {
                consumer(activation);
            }
            catch
            {
                lock (_lock)
                {
                    _isDraining = false;
                }

                throw;
            }

            lock (_lock)
            {
                _pending.Dequeue();
            }
        }
    }
}
