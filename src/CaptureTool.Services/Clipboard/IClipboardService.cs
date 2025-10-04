using System.Threading.Tasks;

namespace CaptureTool.Services.Clipboard;

public partial interface IClipboardService
{
    Task CopyImageAsync(IClipboardImage image);
    Task CopyVideoAsync(IClipboardVideo video);
}
