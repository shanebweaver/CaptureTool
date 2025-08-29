#pragma once

#include <dxgi.h>

extern "C"
{    
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath);
	__declspec(dllexport) void TryPauseRecording();
	__declspec(dllexport) void TryStopRecording();
}