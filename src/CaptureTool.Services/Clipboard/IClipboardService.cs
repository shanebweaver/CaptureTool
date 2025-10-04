using System.Threading.Tasks;

namespace CaptureTool.Services.Clipboard;

public partial interface IClipboardService
{
    Task CopyStreamAsync(IClipboardStream stream);
    Task CopyFileAsync(IClipboardFile file);
}
