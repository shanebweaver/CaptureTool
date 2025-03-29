using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace CaptureTool.Services.Storage;

public interface IJsonStorageService
{
    Task<T?> ReadAsync<T>(string storageItemName, JsonTypeInfo<T> jsonTypeInfo);
    Task WriteAsync<T>(string storageItemName, T value, JsonTypeInfo<T> jsonTypeInfo);
}
