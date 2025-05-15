using CaptureTool.Services.Storage;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Windows.Storage;

namespace CaptureTool.Services.Windows.Storage;

public sealed partial class WindowsJsonStorageService : IJsonStorageService
{
    public async Task<T?> ReadAsync<T>(string filePath, JsonTypeInfo<T> jsonTypeInfo)
    {
        IStorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
        string text = await FileIO.ReadTextAsync(file);
        return JsonSerializer.Deserialize(text, jsonTypeInfo);
    }

    public async Task WriteAsync<T>(string filePath, T value, JsonTypeInfo<T> jsonTypeInfo)
    {
        // Ensure the directory exists
        string? directoryName = Path.GetDirectoryName(filePath);
        if (directoryName != null && !Directory.Exists(directoryName))
        {
            Directory.CreateDirectory(directoryName);
        }

        // Create or replace the file
        string text = JsonSerializer.Serialize(value, jsonTypeInfo);
        await File.WriteAllTextAsync(filePath, text);
    }
}
