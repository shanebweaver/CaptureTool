using CaptureTool.Services.Clipboard;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Services.Windows.Clipboard;

public partial class WindowsClipboardService : IClipboardService
{
    public Task CopyImageAsync(IClipboardImage image)
    {
        using var stream = image.GetStream();
        var randomAccessStream = stream.AsRandomAccessStream();

        var bitmap = RandomAccessStreamReference.CreateFromStream(randomAccessStream);
        var package = new DataPackage();
        package.SetBitmap(bitmap);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();

        return Task.CompletedTask;
    }

    public async Task CopyVideoAsync(IClipboardVideo video)
    {
        if (!File.Exists(video.FilePath))
        {
            return;
        }

        var package = new DataPackage();
        var file = await StorageFile.GetFileFromPathAsync(video.FilePath);
        List<IStorageItem> items = [file];
        package.SetStorageItems(items);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
    }
}