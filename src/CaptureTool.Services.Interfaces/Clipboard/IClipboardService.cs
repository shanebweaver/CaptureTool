namespace CaptureTool.Services.Interfaces.Clipboard;

public partial interface IClipboardService
{
    Task CopyStreamAsync(IClipboardStream stream);
    Task CopyFileAsync(ClipboardFile file);
    Task CopyBitmapAsync(ClipboardFile bitmapFile);
}
