namespace CaptureTool.Application.Abstractions.Windowing;

public partial interface IWindowHandleProvider
{
    nint GetMainWindowHandle();
}
