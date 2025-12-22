#pragma once
#include <windows.h>
#include "CallbackTypes.h"

// Export the callback types and structures from CallbackTypes.h
// These are used at the managed/native boundary

extern "C"
{    
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio = false);
    __declspec(dllexport) void TryPauseRecording();
    __declspec(dllexport) void TryResumeRecording();
    __declspec(dllexport) void TryStopRecording();
    __declspec(dllexport) void TryToggleAudioCapture(bool enabled);
    
    // Callback registration functions
    __declspec(dllexport) void SetVideoFrameCallback(VideoFrameCallback callback);
    __declspec(dllexport) void SetAudioSampleCallback(AudioSampleCallback callback);
}