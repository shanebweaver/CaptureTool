using System.IO;

namespace CaptureTool.Services.Clipboard;

public interface IClipboardStream
{
    Stream GetStream();
}