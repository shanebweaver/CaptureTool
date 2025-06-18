using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinRT;

namespace CaptureTool.Capture.Windows;

public static class GraphicsCaptureHelper
{
    public static async Task<StorageFile> CaptureItemToBitmapFileAsync(GraphicsCaptureItem item, StorageFolder folder, string fileName)
    {
        var d3dDevice = CreateD3DDevice() ?? throw new InvalidOperationException("");
        var direct3DDevice = CreateDirect3DDevice(d3dDevice) ?? throw new InvalidOperationException("");

        var size = item.Size;
        var framePool = Direct3D11CaptureFramePool.Create(
            direct3DDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            size);

        var session = framePool.CreateCaptureSession(item);
        var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();

        framePool.FrameArrived += (s, e) =>
        {
            var frame = s.TryGetNextFrame(); // Don't dispose yet
            tcs.TrySetResult(frame);
            session.Dispose();
            framePool.Dispose();
        };

        session.StartCapture();
        var captureFrame = await tcs.Task;

        var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);
        await SaveFrameToFileAsync(captureFrame, file);
        captureFrame.Dispose(); // Dispose it now

        return file;
    }

    private static ID3D11Device? CreateD3DDevice()
    {
        int hr = D3D11CreateDevice(
            IntPtr.Zero,
            D3D_DRIVER_TYPE.HARDWARE,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            7,
            out IntPtr d3dDevicePtr,
            IntPtr.Zero,
            IntPtr.Zero);

        if (hr != 0)
            Marshal.ThrowExceptionForHR(hr);

        return Marshal.GetObjectForIUnknown(d3dDevicePtr) as ID3D11Device;
    }

    private static IDirect3DDevice CreateDirect3DDevice(ID3D11Device d3dDevice)
    {
        var dxgiDevicePtr = Marshal.GetIUnknownForObject(d3dDevice);
        int hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevicePtr, out IntPtr direct3DDevicePtr);
        Marshal.Release(dxgiDevicePtr);

        if (hr != 0)
            Marshal.ThrowExceptionForHR(hr);

        return MarshalInterface<IDirect3DDevice>.FromAbi(direct3DDevicePtr);
    }

    private static async Task SaveFrameToFileAsync(Direct3D11CaptureFrame frame, StorageFile file)
    {
        using var surface = frame.Surface;
        var bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface);

        using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
    }

    [ComImport]
    [Guid("db6f6ddb-ac77-4e88-8253-819df9bbf140")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11Device
    {
        // Only a marker interface for COM interop; no methods are required for this usage.
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        D3D_DRIVER_TYPE driverType,
        IntPtr Software,
        uint Flags,
        IntPtr pFeatureLevels,
        uint FeatureLevels,
        uint SDKVersion,
        out IntPtr ppDevice,
        IntPtr pFeatureLevel,
        IntPtr ppImmediateContext);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);

    private enum D3D_DRIVER_TYPE
    {
        HARDWARE = 1
    }
}