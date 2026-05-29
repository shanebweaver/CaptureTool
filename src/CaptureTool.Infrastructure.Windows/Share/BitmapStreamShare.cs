using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace CaptureTool.Infrastructure.Windows.Share;

public sealed partial class BitmapStreamShare : IDisposable
{
    private readonly Stream _sourceStream;
    private InMemoryRandomAccessStream? _bitmapStream;
    private DataTransferManager? _manager;

    public BitmapStreamShare(Stream sourceStream)
    {
        _sourceStream = sourceStream;
    }

    public async Task InitializeAsync(DataTransferManager manager)
    {
        _manager = manager;
        _bitmapStream = new InMemoryRandomAccessStream();

        if (_sourceStream.CanSeek)
        {
            _sourceStream.Position = 0;
        }

        await _sourceStream.CopyToAsync(_bitmapStream.AsStreamForWrite());
        _bitmapStream.Seek(0);
        _manager.DataRequested += OnDataRequested;
    }

    private void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs args)
    {
        if (_manager != null)
        {
            _manager.DataRequested -= OnDataRequested;
        }

        DataRequest request = args.Request;
        request.Data.Properties.Title = "Captured Image";
        request.Data.Properties.Description = "Share this capture";

        if (_bitmapStream != null)
        {
            _bitmapStream.Seek(0);
            request.Data.SetBitmap(RandomAccessStreamReference.CreateFromStream(_bitmapStream));
        }
        else
        {
            request.FailWithDisplayText("Failed to load capture bitmap.");
        }
    }

    public void Dispose()
    {
        if (_manager != null)
        {
            _manager.DataRequested -= OnDataRequested;
            _manager = null;
        }

        _bitmapStream?.Dispose();
        _bitmapStream = null;
    }
}
