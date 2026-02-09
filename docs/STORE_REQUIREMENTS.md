# Microsoft Store Requirements for Capture Tool

## Direct3D Requirements

Based on the codebase analysis, Capture Tool requires the following Direct3D capabilities to function properly:

### Hardware Requirements

#### Required Direct3D Features
- **Direct3D Feature Level**: 11.0 or higher
- **Direct3D Version**: DirectX 11 (D3D11)
- **Driver Type**: Hardware-accelerated GPU (D3D_DRIVER_TYPE_HARDWARE)
- **BGRA Support**: D3D11_CREATE_DEVICE_BGRA_SUPPORT flag required

#### Graphics API Support
- **Windows.Graphics.Capture API**: Required for screen capture functionality
- **Windows.Graphics.DirectX.Direct3D11**: Required for frame buffer management
- **DXGI**: DirectX Graphics Infrastructure for device interop

### Windows Requirements
- **Minimum Version**: Windows 10, version 1809 (Build 17763) or later
- **Recommended Version**: Windows 10, version 2004 (Build 19041) or later for optimal Graphics Capture API support

### Win2D Requirements
Capture Tool uses Win2D for image editing operations, which requires:
- **Direct3D 11.0 compatible GPU**: Required for CanvasDevice creation
- **WDDM 2.0 or later**: Windows Display Driver Model for proper D3D11 support

## Partner Center Configuration

When configuring your app in Partner Center, specify the following system requirements:

### Store Listing - System Requirements Section

**DirectX:**
- DirectX 11 Feature Level 11.0 or higher

**Graphics:**
- DirectX 11 compatible graphics card with WDDM 2.0 driver
- Hardware-accelerated GPU with BGRA support

**Operating System:**
- Windows 10, version 1809 (Build 17763) or later

### Technical Specifications

**Minimum Hardware:**
- GPU: DirectX 11 Feature Level 11.0 compatible
- Driver: WDDM 2.0 or later
- Video Memory: 128 MB dedicated video memory

**Recommended Hardware:**
- GPU: DirectX 11.1 or later compatible
- Driver: WDDM 2.1 or later  
- Video Memory: 256 MB or more dedicated video memory

## Device Compatibility Notes

### Known Incompatibilities
Some devices may not support the required Direct3D features even if they meet the minimum Windows version requirements:

1. **Virtual Machines**: May lack hardware acceleration or proper D3D11 support
2. **Legacy Hardware**: Older GPUs that don't support D3D Feature Level 11.0
3. **Server SKUs**: Windows Server editions may have limited graphics capabilities
4. **Remote Desktop**: Remote sessions may not support Windows.Graphics.Capture API

### Specific Device Issues
- **Veriton M200-H510**: Reports indicate this device type may lack proper D3D11 hardware support for Graphics Capture API

## Runtime Detection

Capture Tool implements runtime detection to gracefully handle devices that don't meet the D3D requirements. When an incompatible device is detected:

1. A user-friendly error message is displayed
2. The application provides guidance on system requirements
3. Alternative capture methods may be suggested (if available)

## Additional Resources

- [Direct3D Feature Levels](https://docs.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-devices-downlevel-intro)
- [Windows.Graphics.Capture Namespace](https://docs.microsoft.com/en-us/uwp/api/windows.graphics.capture)
- [Win2D](https://github.com/microsoft/Win2D)
- [WDDM Driver Models](https://docs.microsoft.com/en-us/windows-hardware/drivers/display/windows-vista-and-later-display-driver-model-architecture)
