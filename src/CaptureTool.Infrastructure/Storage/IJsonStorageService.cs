using CaptureTool.Domain.FileSystem;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Infrastructure.Storage;

public interface IJsonStorageService
{
    Task<T?> ReadAsync<T>(FileReference file, JsonTypeInfo<T> jsonTypeInfo);

    Task WriteAsync<T>(FileReference file, T value, JsonTypeInfo<T> jsonTypeInfo);
}
