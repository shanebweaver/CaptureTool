namespace CaptureTool.Infrastructure.Abstractions.Factories;

public interface IFactoryService<T>
{
    T Create();
}
