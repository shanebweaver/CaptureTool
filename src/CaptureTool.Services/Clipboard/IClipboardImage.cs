using System.IO;

namespace CaptureTool.Services.Clipboard;

public interface IClipboardImage
{
    Stream GetStream();
}