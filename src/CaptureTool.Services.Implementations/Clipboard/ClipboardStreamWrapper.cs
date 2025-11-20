using CaptureTool.Services.Interfaces.Clipboard;

namespace CaptureTool.Services.Implementations.Clipboard;

public class ClipboardStreamWrapper : IClipboardStream
{
    private readonly Stream _stream;

    public ClipboardStreamWrapper(Stream stream)
    {
        _stream = stream;
    }

    public Stream GetStream() => _stream;
}
