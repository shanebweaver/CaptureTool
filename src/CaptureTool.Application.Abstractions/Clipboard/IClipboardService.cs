namespace CaptureTool.Application.Abstractions.Clipboard;

public partial interface IClipboardService
{
    Task CopyTextAsync(string text);
    Task CopyStreamAsync(IClipboardStreamSource stream);
    Task CopyFileAsync(ClipboardFile file);
    Task CopyBitmapAsync(ClipboardFile bitmapFile);
}
