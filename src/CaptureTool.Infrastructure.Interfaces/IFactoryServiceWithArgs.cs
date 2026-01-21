namespace CaptureTool.Infrastructure.Interfaces;

public interface IFactoryServiceWithArgs<T, A>
{
    T Create(A args);
}
