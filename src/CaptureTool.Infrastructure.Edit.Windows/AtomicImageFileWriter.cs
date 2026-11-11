using Windows.Storage;
using Windows.Storage.Provider;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Edit.Windows;

internal static class AtomicImageFileWriter
{
    public static async Task WriteAsync(string filePath, Func<IRandomAccessStream, Task> writeAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(writeAsync);

        string destinationPath = Path.GetFullPath(filePath);
        StorageFile file;
        bool createdFile = false;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(destinationPath);
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            string directoryPath = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException("The image destination does not have a parent directory.");
            Directory.CreateDirectory(directoryPath);
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(directoryPath);
            file = await folder.CreateFileAsync(Path.GetFileName(destinationPath), CreationCollisionOption.FailIfExists);
            createdFile = true;
        }

        bool committed = false;
        try
        {
            // Coordinate updates on the destination so its storage provider owns the save.
            // Local files have no cached-file updater (including in unpackaged test hosts).
            bool deferUpdates = file.Provider.Id != "computer";
            if (deferUpdates)
            {
                CachedFileManager.DeferUpdates(file);
            }
            try
            {
                // A WinRT transaction stages changes without moving a sibling file over a
                // provider-managed destination. Disposing without committing preserves it.
                using StorageStreamTransaction transaction = await file.OpenTransactedWriteAsync();
                IRandomAccessStream stream = transaction.Stream;
                stream.Size = 0;
                stream.Seek(0);
                await writeAsync(stream);
                if (!await stream.FlushAsync())
                {
                    throw new IOException($"Failed to flush image '{destinationPath}'.");
                }

                await transaction.CommitAsync();
                committed = true;
            }
            catch
            {
                if (deferUpdates)
                {
                    try
                    {
                        await CompleteUpdatesAsync(file);
                    }
                    catch
                    {
                        // Release the update deferral without hiding the original write failure.
                    }
                }
                throw;
            }

            if (deferUpdates)
            {
                await CompleteUpdatesAsync(file);
            }
        }
        finally
        {
            if (createdFile && !committed)
            {
                try
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
                catch
                {
                    // Cleanup of a newly created file must not hide the save failure.
                }
            }
        }
    }

    private static async Task CompleteUpdatesAsync(StorageFile file)
    {
        try
        {
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status != FileUpdateStatus.Complete)
            {
                throw new IOException($"Failed to complete updates for image '{file.Path}': {status}.");
            }
        }
        catch (System.Runtime.InteropServices.COMException exception)
        {
            throw new IOException($"Failed to complete updates for image '{file.Path}' (0x{exception.HResult:X8}).", exception);
        }
    }
}
