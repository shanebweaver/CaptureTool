namespace CaptureTool.Infrastructure.Interfaces.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
    Task ShareStreamAsync(Stream stream, nint hwnd);
}
