#pragma once
#include "AudioDeviceEnumerator.h"
#include "SourceManager.h"

extern "C"
{    
	// New source-based recording with microphone support (4 parameters)
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureDesktopAudio, bool captureMicrophone);
	
	// Legacy signature for backward compatibility (3 parameters)
	__declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio = false);
	
	__declspec(dllexport) void TryPauseRecording();
	__declspec(dllexport) void TryResumeRecording();
	__declspec(dllexport) void TryStopRecording();
	__declspec(dllexport) void TryToggleAudioCapture(bool enabled);
	
	// Audio device enumeration
	__declspec(dllexport) int EnumerateAudioCaptureDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) int EnumerateAudioRenderDevices(AudioDeviceInfo** devices);
	__declspec(dllexport) void FreeAudioDeviceInfo(AudioDeviceInfo* devices);
	
	// Source management exports
	__declspec(dllexport) SourceHandle RegisterVideoSource(void* sourcePtr);
	__declspec(dllexport) SourceHandle RegisterAudioSource(void* sourcePtr);
	__declspec(dllexport) void UnregisterSource(SourceHandle handle);
	__declspec(dllexport) bool StartAllSources();
	__declspec(dllexport) void StopAllSources();
	__declspec(dllexport) int GetSourceCount();
}