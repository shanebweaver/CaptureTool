namespace CaptureTool.MetadataScanner.Windows.WinUI.Services;

public sealed class WindowHandleProvider : IWindowHandleProvider
{
    public IntPtr WindowHandle { get; private set; }

    public void SetWindowHandle(IntPtr windowHandle)
    {
        WindowHandle = windowHandle;
    }
}
