namespace CaptureTool.Services;

public interface IFactoryService<T>
{
    T Create();
}

public interface IFactoryService<T, A>
{
    T Create(A args);
}
