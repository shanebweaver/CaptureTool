namespace CaptureTool.Infrastructure.Edit.Windows;

internal static class AtomicImageFileWriter
{
    public static async Task WriteAsync(string filePath, Func<string, Task> writeTemporaryFileAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(writeTemporaryFileAsync);

        string destinationPath = Path.GetFullPath(filePath);
        string directoryPath = Path.GetDirectoryName(destinationPath)
            ?? throw new InvalidOperationException("The image destination does not have a parent directory.");
        Directory.CreateDirectory(directoryPath);

        string temporaryFilePath = Path.Combine(
            directoryPath,
            $".{Path.GetFileNameWithoutExtension(destinationPath)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(destinationPath)}");

        try
        {
            using (FileStream _ = new(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }

            await writeTemporaryFileAsync(temporaryFilePath);
            File.Move(temporaryFilePath, destinationPath, true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryFilePath);
            }
            catch
            {
                // Temporary-file cleanup is best effort and must not hide the save failure.
            }
        }
    }
}
