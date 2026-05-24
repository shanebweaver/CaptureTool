namespace CaptureTool.Infrastructure.Abstractions.Factories;

public interface IFactoryServiceWithArgs<T, A>
{
    T Create(A args);
}
