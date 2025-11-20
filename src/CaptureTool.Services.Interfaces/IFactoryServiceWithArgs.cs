namespace CaptureTool.Services.Interfaces;

public interface IFactoryServiceWithArgs<T, A>
{
    T Create(A args);
}
