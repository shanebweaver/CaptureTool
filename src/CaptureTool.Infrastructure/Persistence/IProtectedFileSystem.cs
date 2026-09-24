namespace CaptureTool.Infrastructure.Persistence;

/// <summary>Internal IO seam for deterministic publication and cleanup failure tests.</summary>
internal interface IProtectedFileSystem
{
    Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken);
    Task WriteAtomicallyAsync(string path, byte[] ciphertext, CancellationToken cancellationToken);
    string[] GetFiles(string directory);
    string[] GetDirectories(string directory);
    void DeleteFile(string path);
    void DeleteDocumentDirectory(string directory);
}
