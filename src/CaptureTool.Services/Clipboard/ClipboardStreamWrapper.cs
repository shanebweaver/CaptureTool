using System.IO;

namespace CaptureTool.Services.Clipboard;

public class ClipboardStreamWrapper : IClipboardStream
{
    private readonly Stream _stream;

    public ClipboardStreamWrapper(Stream stream)
    {
        _stream = stream;
    }

    public Stream GetStream() => _stream;
}
