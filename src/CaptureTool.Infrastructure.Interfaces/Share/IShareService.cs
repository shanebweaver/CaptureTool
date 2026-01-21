namespace CaptureTool.Infrastructure.Interfaces.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
}
