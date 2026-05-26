using CaptureTool.Infrastructure.Abstractions.Commands;

namespace CaptureTool.Infrastructure.Commands;

/// <summary>
/// Implementation of <see cref="IAsyncAppCommand{T}"/> that executes an asynchronous delegate with a parameter.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public class AsyncAppCommand<T> : IAsyncAppCommand<T>
{
    public static AsyncAppCommand<T> Create(Func<T?, Task> execute)
    {
        return new AsyncAppCommand<T>(execute);
    }

    public static AsyncAppCommand<T> Create(Func<T?, Task> execute, Predicate<T?> canExecute)
    {
        return new AsyncAppCommand<T>(execute, canExecute);
    }

    protected AsyncAppCommand(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    private readonly Func<T?, Task> _execute;
    private readonly Predicate<T?>? _canExecute;
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

    public async Task ExecuteAsync(T? parameter, CancellationToken cancellationToken)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        IsExecuting = true;
        try
        {
            Task t = _execute(parameter);
            await t.WaitAsync(cancellationToken);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    public bool CanExecute(T? parameter)
    {
        return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event.
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
