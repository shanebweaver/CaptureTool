namespace CaptureTool.Application.Abstractions.Share;

public partial interface IShareService
{
    Task ShareAsync(string filePath);
    Task ShareStreamAsync(Stream stream);
}
