using CaptureTool.Application.Abstractions.Files;

namespace CaptureTool.Application.Capture;

internal sealed class CaptureFileAllocator
{
    internal const int MaxAttempts = 10;

    private readonly IFileSystem _fileSystem;

    public CaptureFileAllocator(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public string ReserveUniqueFile(string folderPath, Func<string> createFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(createFileName);

        return ExecuteWithUniquePath(
            folderPath,
            createFileName,
            path => _fileSystem.CreateEmptyFile(path));
    }

    public string CopyToUniqueFile(
        string sourcePath,
        string folderPath,
        Func<string> createFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        ArgumentNullException.ThrowIfNull(createFileName);

        return ExecuteWithUniquePath(
            folderPath,
            createFileName,
            path => _fileSystem.CopyFile(sourcePath, path, overwrite: false));
    }

    public void TryDeleteFile(string filePath)
    {
        try
        {
            _fileSystem.DeleteFile(filePath);
        }
        catch
        {
            // Cleanup must not replace the original capture failure.
        }
    }

    private string ExecuteWithUniquePath(
        string folderPath,
        Func<string> createFileName,
        Action<string> createFile)
    {
        _fileSystem.CreateDirectory(folderPath);
        IOException? lastCollision = null;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            string filePath = Path.Combine(folderPath, createFileName());

            try
            {
                createFile(filePath);
                return filePath;
            }
            catch (IOException exception)
            {
                if (!_fileSystem.FileExists(filePath))
                {
                    throw;
                }

                lastCollision = exception;
            }
        }

        throw new IOException(
            $"Could not allocate a unique capture file after {MaxAttempts} attempts.",
            lastCollision);
    }
}
