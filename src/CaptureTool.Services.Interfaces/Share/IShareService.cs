namespace CaptureTool.Services.Interfaces.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
}
