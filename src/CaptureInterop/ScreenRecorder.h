#pragma once
#include "AudioDeviceEnumerator.h"

extern "C"
{    
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio = false);
	__declspec(dllexport) void TryPauseRecording();
	__declspec(dllexport) void TryResumeRecording();
	__declspec(dllexport) void TryStopRecording();
	__declspec(dllexport) void TryToggleAudioCapture(bool enabled);
	
	// Audio device enumeration
	__declspec(dllexport) int EnumerateAudioCaptureDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) int EnumerateAudioRenderDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) void FreeAudioDeviceInfo(AudioDeviceInfo* devices);
}