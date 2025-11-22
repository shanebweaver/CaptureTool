using CaptureTool.Services.Interfaces.Clipboard;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Services.Implementations.Windows.Clipboard;

public partial class WindowsClipboardService : IClipboardService
{
    public Task CopyStreamAsync(IClipboardStream clipboardStream)
    {
        using var stream = clipboardStream.GetStream();
        var randomAccessStream = stream.AsRandomAccessStream();

        var bitmap = RandomAccessStreamReference.CreateFromStream(randomAccessStream);
        var package = new DataPackage();
        package.SetBitmap(bitmap);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();

        return Task.CompletedTask;
    }

    public async Task CopyFileAsync(IClipboardFile clipboardFile)
    {
        if (!File.Exists(clipboardFile.FilePath))
        {
            return;
        }

        var package = new DataPackage();
        var file = await StorageFile.GetFileFromPathAsync(clipboardFile.FilePath);
        List<IStorageItem> items = [file];
        package.SetStorageItems(items);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
    }
}