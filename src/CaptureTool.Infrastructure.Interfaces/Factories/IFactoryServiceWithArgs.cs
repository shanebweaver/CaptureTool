namespace CaptureTool.Infrastructure.Interfaces.Factories;

public interface IFactoryServiceWithArgs<T, A>
{
    T Create(A args);
}
