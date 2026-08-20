using CaptureTool.Application.Abstractions.Windowing;

namespace CaptureTool.Infrastructure.Windows.Windowing;

internal sealed class MainWindowActivationService : IMainWindowActivationService
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _activatedCompletionSource = CreateCompletionSource();
    private bool _isActive;

    public Task WaitUntilActivatedAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isActive)
            {
                return Task.CompletedTask;
            }

            Task activationTask = _activatedCompletionSource.Task;
            return cancellationToken.CanBeCanceled
                ? activationTask.WaitAsync(cancellationToken)
                : activationTask;
        }
    }

    public void SetActive(bool isActive)
    {
        TaskCompletionSource? completionSource = null;
        lock (_lock)
        {
            if (_isActive == isActive)
            {
                return;
            }

            _isActive = isActive;
            if (isActive)
            {
                completionSource = _activatedCompletionSource;
            }
            else
            {
                _activatedCompletionSource = CreateCompletionSource();
            }
        }

        completionSource?.TrySetResult();
    }

    private static TaskCompletionSource CreateCompletionSource()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
