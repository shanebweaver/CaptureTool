using System.Threading.Tasks;

namespace CaptureTool.Services.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
}
