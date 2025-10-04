using System.IO;

namespace CaptureTool.Services.Clipboard;

public class ClipboardImageWrapper : IClipboardImage
{
    private readonly Stream _stream;

    public ClipboardImageWrapper(Stream stream)
    {
        _stream = stream;
    }

    public Stream GetStream() => _stream;
}
