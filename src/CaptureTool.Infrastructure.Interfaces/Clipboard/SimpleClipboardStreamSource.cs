namespace CaptureTool.Infrastructure.Interfaces.Clipboard;

public sealed partial class SimpleClipboardStreamSource : IClipboardStreamSource
{
    private readonly Stream _stream;

    public SimpleClipboardStreamSource(Stream stream)
    {
        _stream = stream;
    }

    public Stream GetStream() => _stream;
}
