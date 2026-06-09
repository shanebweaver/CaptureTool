using CaptureTool.Application.Abstractions.Clipboard;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Windows.Clipboard;

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

        InMemoryRandomAccessStream inMemoryStream = await CreateClipboardBitmapStreamAsync(stream);

        // Prepare clipboard content
        DataPackage dataPackage = new();
        dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(inMemoryStream));
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
    }

    public async Task CopyStreamAsync(IClipboardStreamSource streamSource)
    {
        using var stream = streamSource.GetStream();
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using IRandomAccessStream randomAccessStream = stream.AsRandomAccessStream();
        InMemoryRandomAccessStream inMemoryStream = await CreateClipboardBitmapStreamAsync(randomAccessStream);

        var bitmap = RandomAccessStreamReference.CreateFromStream(inMemoryStream);
        var package = new DataPackage();
        package.SetBitmap(bitmap);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        global::Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
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

    private static async Task<InMemoryRandomAccessStream> CreateClipboardBitmapStreamAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);

        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
        using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        using SoftwareBitmap converted = SoftwareBitmap.Convert(
            softwareBitmap,
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied);

        InMemoryRandomAccessStream inMemoryStream = new();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, inMemoryStream);
        encoder.SetSoftwareBitmap(converted);
        await encoder.FlushAsync();
        inMemoryStream.Seek(0);

        return inMemoryStream;
    }
}
