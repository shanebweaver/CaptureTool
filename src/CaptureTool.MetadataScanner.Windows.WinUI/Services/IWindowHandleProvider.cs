namespace CaptureTool.MetadataScanner.Windows.WinUI.Services;

public interface IWindowHandleProvider
{
    IntPtr WindowHandle { get; }

    void SetWindowHandle(IntPtr windowHandle);
}
