using CaptureTool.Infrastructure.Interfaces.Capabilities;
using System.Runtime.InteropServices;
using Microsoft.Graphics.Canvas;

namespace CaptureTool.Infrastructure.Implementations.Windows.Capabilities;

/// <summary>
/// Windows implementation of D3D capability checking using Direct3D 11 and Win2D.
/// </summary>
public sealed class D3DCapabilityService : ID3DCapabilityService
{
    private const int D3D_FEATURE_LEVEL_11_0 = 0xb000;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private const uint D3D11_SDK_VERSION = 7;

    // Common HRESULT values
    private const int DXGI_ERROR_UNSUPPORTED = unchecked((int)0x887A0001);
    private const int E_FAIL = unchecked((int)0x80004005);
    private const int E_INVALIDARG = unchecked((int)0x80070057);

    [DllImport("d3d11.dll", SetLastError = false, CharSet = CharSet.Unicode)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int driverType,
        IntPtr software,
        uint flags,
        [In] int[]? pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out IntPtr ppDevice,
        IntPtr pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetLastError();

    /// <inheritdoc/>
    public D3DCapabilityCheckResult CheckD3DCapabilities()
    {
        // Test 1: Check if D3D11 device can be created with required feature level
        var d3dResult = CheckD3D11Device();
        if (!d3dResult.IsSupported)
        {
            return d3dResult;
        }

        // Test 2: Check if Win2D CanvasDevice can be created
        var win2dResult = CheckWin2DDevice();
        if (!win2dResult.IsSupported)
        {
            return win2dResult;
        }

        return D3DCapabilityCheckResult.Success();
    }

    /// <inheritdoc/>
    public string GetUnsupportedDeviceMessage()
    {
        return "Your device does not support the required graphics features for Capture Tool.\n\n" +
               "Required features:\n" +
               "• DirectX 11 Feature Level 11.0 or higher\n" +
               "• Hardware-accelerated GPU with BGRA support\n" +
               "• Windows.Graphics.Capture API support\n\n" +
               "Please ensure:\n" +
               "• Your graphics drivers are up to date\n" +
               "• Your GPU supports DirectX 11\n" +
               "• Hardware acceleration is enabled in your system settings\n\n" +
               "For more information, see the system requirements in the Microsoft Store.";
    }

    private D3DCapabilityCheckResult CheckD3D11Device()
    {
        IntPtr device = IntPtr.Zero;
        IntPtr context = IntPtr.Zero;

        try
        {
            int[] featureLevels = { D3D_FEATURE_LEVEL_11_0 };

            int hr = D3D11CreateDevice(
                IntPtr.Zero,
                D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                featureLevels,
                (uint)featureLevels.Length,
                D3D11_SDK_VERSION,
                out device,
                IntPtr.Zero,
                out context);

            if (hr < 0)
            {
                string errorMessage = hr switch
                {
                    DXGI_ERROR_UNSUPPORTED => "The hardware does not support the requested DirectX feature level or BGRA format.",
                    E_INVALIDARG => "Invalid Direct3D parameters. The system may not support hardware acceleration.",
                    E_FAIL => "Failed to create Direct3D 11 device. The graphics hardware may be incompatible or drivers may be missing.",
                    _ => $"Failed to create Direct3D 11 device. HRESULT: 0x{hr:X8}"
                };

                return D3DCapabilityCheckResult.Failure(
                    "Direct3D 11 Feature Level 11.0 with BGRA support",
                    errorMessage,
                    hr);
            }

            return D3DCapabilityCheckResult.Success();
        }
        catch (DllNotFoundException)
        {
            return D3DCapabilityCheckResult.Failure(
                "Direct3D 11 Library",
                "Direct3D 11 library (d3d11.dll) not found. DirectX 11 runtime may not be installed.");
        }
        catch (Exception ex)
        {
            return D3DCapabilityCheckResult.Failure(
                "Direct3D 11 Device Creation",
                $"Unexpected error during D3D11 device creation: {ex.Message}");
        }
        finally
        {
            // Release COM objects
            if (device != IntPtr.Zero)
            {
                Marshal.Release(device);
            }
            if (context != IntPtr.Zero)
            {
                Marshal.Release(context);
            }
        }
    }

    private D3DCapabilityCheckResult CheckWin2DDevice()
    {
        try
        {
            // Attempt to get or create a Win2D CanvasDevice
            // This validates that the system can create a device for Win2D operations
            using CanvasDevice? device = CanvasDevice.GetSharedDevice();
            
            if (device == null)
            {
                return D3DCapabilityCheckResult.Failure(
                    "Win2D CanvasDevice",
                    "Failed to create Win2D CanvasDevice. The system may not support the required graphics features for image editing.");
            }

            return D3DCapabilityCheckResult.Success();
        }
        catch (Exception ex)
        {
            string errorMessage = ex.Message.Contains("0x887A0001", StringComparison.OrdinalIgnoreCase)
                ? "Win2D device creation failed with DXGI_ERROR_UNSUPPORTED. The GPU may not support the required DirectX features."
                : $"Failed to create Win2D device: {ex.Message}";

            return D3DCapabilityCheckResult.Failure(
                "Win2D CanvasDevice",
                errorMessage);
        }
    }
}
