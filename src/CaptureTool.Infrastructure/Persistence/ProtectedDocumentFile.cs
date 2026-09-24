using CaptureTool.Application.Abstractions.Security;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CaptureTool.Infrastructure.Persistence;

internal sealed class ProtectedDocumentFile
{
    private readonly IUserDataProtector _protector;
    private readonly IProtectedFileSystem _files;

    public ProtectedDocumentFile(IUserDataProtector protector, IProtectedFileSystem files)
    {
        _protector = protector;
        _files = files;
    }

    public async Task<T?> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken) where T : class
    {
        byte[]? ciphertext = await _files.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (ciphertext == null) return null;
        byte[] plaintext = _protector.Unprotect(ciphertext);
        try
        {
            return JsonSerializer.Deserialize(plaintext, typeInfo) ?? throw new InvalidDataException("Empty protected document.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid protected document format.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async Task WriteAsync<T>(string path, T document, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(document, typeInfo);
        byte[] ciphertext;
        try { ciphertext = _protector.Protect(plaintext); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        if (ciphertext.Length == 0) throw new CryptographicException("Protection returned an empty document.");
        await _files.WriteAtomicallyAsync(path, ciphertext, cancellationToken).ConfigureAwait(false);
    }
}
