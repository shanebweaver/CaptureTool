using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Infrastructure.Commands;

/// <summary>
/// Implementation of <see cref="IAsyncAppCommand"/> that executes an asynchronous delegate.
/// </summary>
public class AsyncAppCommand : IAsyncAppCommand
{
    public static AsyncAppCommand Create(Func<Task> execute)
    {
        return new AsyncAppCommand(execute);
    }

    public static AsyncAppCommand Create(Func<Task> execute, Func<bool> canExecute)
    {
        return new AsyncAppCommand(execute, canExecute);
    }

    protected AsyncAppCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            if (_isExecuting != value)
            {
                _isExecuting = value;
                RaiseCanExecuteChanged();
            }
        }
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!CanExecute())
        {
            return;
        }

        IsExecuting = true;
        try
        {
            Task t = _execute();
            await t.WaitAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public bool CanExecute()
    {
        return !IsExecuting && (_canExecute?.Invoke() ?? true);
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
