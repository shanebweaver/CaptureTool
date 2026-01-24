using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.ViewModels;

public abstract partial class AsyncLoadableViewModelBase<T> : ViewModelBase, IAsyncLoadable<T>
{
    public Type ParameterType => typeof(T);

    public async Task LoadAsync(object? parameter, CancellationToken cancellationToken)
    {
        if (parameter is null)
        {
            if (default(T) is null)
            {
                await LoadAsync(parameter, cancellationToken);
                return;
            }

            throw new InvalidOperationException($"Parameter was null but target type {ParameterType?.FullName} is non-nullable.");
        }

        if (parameter is T t)
        {
            await LoadAsync(t, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Parameter is not of expected type {ParameterType?.FullName}.");
    }

    public virtual Task LoadAsync(T parameter, CancellationToken cancellationToken)
    {
        LoadingComplete();
        return Task.CompletedTask;
    }
}