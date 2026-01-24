using CaptureTool.Infrastructure.Interfaces.Loading;

namespace CaptureTool.Infrastructure.Implementations.Loading;

public abstract partial class LoadableWithParam<T> : HasLoadStateBase, ILoadable<T>
{
    public Type ParameterType => typeof(T);

    public void Load(object? parameter)
    {
        if (parameter is null)
        {
            if (default(T) is null)
            {
                Load(default!);
                return;
            }

            throw new InvalidOperationException($"Parameter was null but target type {ParameterType?.FullName} is non-nullable.");
        }

        if (parameter is T t)
        {
            Load(t);
            return;
        }

        throw new InvalidOperationException($"Parameter is not of expected type {ParameterType?.FullName}.");
    }

    public virtual void Load(T parameter) => LoadingComplete();
}
