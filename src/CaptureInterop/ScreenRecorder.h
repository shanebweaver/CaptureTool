#pragma once
#include <windows.h>
#include "CallbackTypes.h"
#include "CaptureOperationResult.h"

// Export the callback types and structures from CallbackTypes.h
// These are used at the managed/native boundary

extern "C"
{    
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio = false) noexcept;
    __declspec(dllexport) void TryPauseRecording() noexcept;
    __declspec(dllexport) void TryResumeRecording() noexcept;
    __declspec(dllexport) HRESULT TryStopRecording(CaptureOperationStage* outStage) noexcept;
    __declspec(dllexport) void TryToggleAudioCapture(bool enabled) noexcept;
    
    // Callback registration functions
    __declspec(dllexport) void SetVideoFrameCallback(VideoFrameCallback callback) noexcept;
    __declspec(dllexport) void SetAudioSampleCallback(AudioSampleCallback callback) noexcept;
}
