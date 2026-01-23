using CaptureTool.Infrastructure.Interfaces.Commands;

namespace CaptureTool.Infrastructure.Implementations.Commands;

/// <summary>
/// Implementation of <see cref="IAsyncAppCommand"/> that executes an asynchronous delegate.
/// </summary>
public sealed class AsyncAppCommand : IAsyncAppCommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncAppCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

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

    public async Task ExecuteAsync()
    {
        if (!CanExecute())
        {
            return;
        }

        IsExecuting = true;
        try
        {
            await _execute();
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
