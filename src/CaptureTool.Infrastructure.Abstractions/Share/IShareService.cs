namespace CaptureTool.Infrastructure.Abstractions.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath, nint hwnd);
}
