using CaptureTool.Services.Interfaces.Clipboard;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Services.Implementations.Windows.Clipboard;

public partial class WindowsClipboardService : IClipboardService
{
    public async Task CopyBitmapAsync(ClipboardFile clipboardFile)
    {
        if (!File.Exists(clipboardFile.FilePath))
        {
            return;
        }

        // Load the file
        StorageFile file = await StorageFile.GetFileFromPathAsync(clipboardFile.FilePath);

        // Open the file as a stream
        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);

        // Decode the bitmap
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();

        // Convert to a compatible format (Clipboard requires BGRA8 with premultiplied alpha)
        SoftwareBitmap converted = SoftwareBitmap.Convert(
            softwareBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied
        );

        // Encode to PNG into a stream
        InMemoryRandomAccessStream inMemoryStream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryStream);
        encoder.SetSoftwareBitmap(converted);
        await encoder.FlushAsync();

        // Prepare clipboard content
        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(inMemoryStream));
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
    }

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

    public async Task CopyFileAsync(ClipboardFile clipboardFile)
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