namespace CaptureTool.Infrastructure.Abstractions.Windowing;

public partial interface IWindowHandleProvider
{
    nint GetMainWindowHandle();
}
