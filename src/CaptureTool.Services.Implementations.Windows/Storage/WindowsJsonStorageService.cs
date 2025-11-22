using CaptureTool.Services.Interfaces.Storage;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Windows.Storage;

namespace CaptureTool.Services.Implementations.Windows.Storage;

public sealed partial class WindowsJsonStorageService : IJsonStorageService
{
    public async Task<T?> ReadAsync<T>(IFile file, JsonTypeInfo<T> jsonTypeInfo)
    {
        if (!File.Exists(file.FilePath))
        {
            return default;
        }

        IStorageFile storageFile = await StorageFile.GetFileFromPathAsync(file.FilePath);
        string text = await FileIO.ReadTextAsync(storageFile);
        return JsonSerializer.Deserialize(text, jsonTypeInfo);
    }

    public async Task WriteAsync<T>(IFile file, T value, JsonTypeInfo<T> jsonTypeInfo)
    {
        // Ensure the directory exists
        string? directoryName = Path.GetDirectoryName(file.FilePath);
        if (directoryName != null && !Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        // Create or replace the file
        string text = JsonSerializer.Serialize(value, jsonTypeInfo);
        await File.WriteAllTextAsync(file.FilePath, text);
    }
}
