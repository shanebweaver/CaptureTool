// pch.h: This is a precompiled header file.
// Files listed below are compiled only once, improving build performance for future builds.
// This also affects IntelliSense performance, including code completion and many code browsing features.
// However, files listed here are ALL re-compiled if any one of them is updated between builds.
// Do not add files here that you will be updating frequently as this negates the performance advantage.

#ifndef PCH_H
#define PCH_H

// add headers that you want to pre-compile here
#include "framework.h"

// WinRT
#include <roapi.h>

// DirectX
#include <d3d11.h>
#include <dxgi.h>

// Media Foundation
#include <mfapi.h>
#include <mfobjects.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <mferror.h>
#include <codecapi.h>
#include <wmcodecdsp.h>

// Link Media Foundation libraries
#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfreadwrite.lib")
#pragma comment(lib, "mfuuid.lib")  // Contains CLSID definitions for MF codecs
#pragma comment(lib, "wmcodecdspuuid.lib")  // Contains CLSID definitions for resampler

// Audio Capture (WASAPI)
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <functiondiscoverykeys_devpkey.h>
#include <mmreg.h>  // For WAVE_FORMAT constants
#include <ksmedia.h>  // For KSDATAFORMAT_SUBTYPE constants

// Link WASAPI libraries
#pragma comment(lib, "uuid.lib")

// Windows Implementation Library
#include <wil/com.h>

// Standard Library
#include <thread>
#include <atomic>
#include <mutex>
#include <vector>
#include <map>
#include <string>

// Windows ABI
#include <windows.foundation.h>
#include <windows.graphics.capture.h>
#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#endif //PCH_H
