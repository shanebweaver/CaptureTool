namespace CaptureTool.Presentation.Factories;

public interface IFactoryServiceWithArgs<T, A>
{
    T Create(A args);
}
