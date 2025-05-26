/*using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Graphics.Capture;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Composition;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;

namespace CaptureTool.Capture.Windows;

public static class MonitorCaptureHelper
{
    // IGraphicsCaptureItemInterop GUID
    private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628e81b-3cac-4c60-b7f4-23ce0e0c3356");

    [ComImport]
    [Guid("3628e81b-3cac-4c60-b7f4-23ce0e0c3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr hwnd, ref Guid iid);
        IntPtr CreateForMonitor(IntPtr hmon, ref Guid iid);
    }

    public static async Task<CanvasBitmap?> CaptureMonitorToBitmapAsync(IntPtr hMonitor)
    {
        // Get the IGraphicsCaptureItemInterop interface
        var factory = typeof(GraphicsCaptureItem).GetActivationFactory();
        var interop = (IGraphicsCaptureItemInterop)factory;

        // Create the capture item for the monitor
        var iid = typeof(GraphicsCaptureItem).GetInterface("IGraphicsCaptureItem").GUID;
        var itemPtr = interop.CreateForMonitor(hMonitor, ref iid);
        var captureItem = MarshalInterface<GraphicsCaptureItem>(itemPtr);

        // Create a Direct3D device
        var device = CanvasDevice.GetSharedDevice();
        var d3dDevice = CanvasDeviceExtensions.AsDirect3D11Device(device);

        // Create a frame pool and session
        var framePool = Direct3D11CaptureFramePool.Create(
            d3dDevice,
            Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
            1,
            captureItem.Size);

        var session = framePool.CreateCaptureSession(captureItem);

        // Start capture and get a frame
        var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>();
        void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            if (frame != null)
            {
                tcs.TrySetResult(frame);
            }
        }
        framePool.FrameArrived += OnFrameArrived;
        session.StartCapture();

        // Wait for the first frame
        var captureFrame = await tcs.Task.ConfigureAwait(false);

        // Create a CanvasBitmap from the frame's surface
        var canvasBitmap = CanvasBitmap.CreateFromDirect3D11Surface(device, captureFrame.Surface);

        // Cleanup
        session.Dispose();
        framePool.Dispose();

        return canvasBitmap;
    }

    // Helper to marshal WinRT interface pointer to managed object
    private static T MarshalInterface<T>(IntPtr ptr) where T : class
    {
        if (ptr == IntPtr.Zero) throw new ArgumentNullException(nameof(ptr));
        var obj = Marshal.GetObjectForIUnknown(ptr);
        Marshal.Release(ptr);
        return (T)obj;
    }
}*/