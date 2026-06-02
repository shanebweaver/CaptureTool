using CaptureTool.Domain.Capture.Files;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Application.Abstractions.Storage;

public interface IJsonStorageService
{
    Task<T?> ReadAsync<T>(IFile file, JsonTypeInfo<T> jsonTypeInfo);

    Task WriteAsync<T>(IFile file, T value, JsonTypeInfo<T> jsonTypeInfo);
}
