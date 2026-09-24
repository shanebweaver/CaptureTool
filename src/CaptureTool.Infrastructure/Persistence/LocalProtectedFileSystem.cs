namespace CaptureTool.Infrastructure.Persistence;

internal sealed class LocalProtectedFileSystem : IProtectedFileSystem
{
    private const long MaximumDocumentBytes = 64 * 1024 * 1024;

    public async Task<byte[]?> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true);
            if (stream.Length > MaximumDocumentBytes) throw new InvalidDataException("Analysis document exceeds its size limit.");
            byte[] data = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(data, cancellationToken).ConfigureAwait(false);
            return data;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    public async Task WriteAtomicallyAsync(string path, byte[] ciphertext, CancellationToken cancellationToken)
    {
        if (ciphertext.LongLength > MaximumDocumentBytes) throw new InvalidDataException("Analysis document exceeds its size limit.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await stream.WriteAsync(ciphertext, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(temporaryPath, path, destinationBackupFileName: null);
            else File.Move(temporaryPath, path);
        }
        finally
        {
            // Only ciphertext reaches this file. A locked leftover can be cleaned on a later clear.
            try { File.Delete(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public string[] GetFiles(string directory) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory) : [];

    public string[] GetDirectories(string directory) =>
        Directory.Exists(directory) ? Directory.GetDirectories(directory) : [];

    public void DeleteFile(string path) => File.Delete(path);

    public void DeleteDocumentDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Refusing to traverse an analysis generation link.");

        // Generations contain documents only. Never recursively traverse an unexpected directory.
        foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory, recursive: false);
    }
}
