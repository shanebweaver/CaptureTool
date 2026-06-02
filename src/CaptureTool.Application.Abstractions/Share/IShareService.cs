namespace CaptureTool.Application.Abstractions.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
    Task ShareStreamAsync(Stream stream, nint hwnd);
}
