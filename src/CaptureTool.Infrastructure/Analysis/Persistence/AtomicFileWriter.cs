namespace CaptureTool.Infrastructure.Analysis.Persistence;

internal interface IAtomicFileWriter
{
    void Write(string destinationPath, ReadOnlySpan<byte> contents);
}

internal sealed class AtomicFileWriter : IAtomicFileWriter
{
    public void Write(string destinationPath, ReadOnlySpan<byte> contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        string directoryPath = Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException("A destination directory is required.", nameof(destinationPath));
        Directory.CreateDirectory(directoryPath);

        string temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch
            {
                // A leftover same-directory temporary file is never treated as committed state.
            }
        }
    }
}
