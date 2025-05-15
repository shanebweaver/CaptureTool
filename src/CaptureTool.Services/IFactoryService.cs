using System.Threading;
using System.Threading.Tasks;

namespace CaptureTool.Services;

public interface IFactoryService<T>
{
    T Create();
}

public interface IFactoryService<T, A>
{
    Task<T> CreateAsync(A args, CancellationToken cancellationToken);
}
