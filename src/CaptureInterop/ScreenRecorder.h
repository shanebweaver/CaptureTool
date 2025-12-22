#pragma once
#include <windows.h>

// Callback data structures for managed layer
struct VideoFrameData
{
    void* pTexture;         // Pointer to ID3D11Texture2D
    LONGLONG timestamp;     // Timestamp in 100ns ticks
    UINT32 width;           // Frame width in pixels
    UINT32 height;          // Frame height in pixels
};

struct AudioSampleData
{
    const BYTE* pData;      // Pointer to audio sample data
    UINT32 numFrames;       // Number of audio frames
    LONGLONG timestamp;     // Timestamp in 100ns ticks
    UINT32 sampleRate;      // Sample rate in Hz
    UINT16 channels;        // Number of channels
    UINT16 bitsPerSample;   // Bits per sample
};

// Callback function types for managed layer
using VideoFrameCallback = void(__stdcall*)(const VideoFrameData* pFrameData);
using AudioSampleCallback = void(__stdcall*)(const AudioSampleData* pSampleData);

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