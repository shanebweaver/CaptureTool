#pragma once
#include <windows.h>

// Callback data structures for managed/native boundary
// These structures must match the C# definitions exactly for proper marshaling

/// <summary>
/// Video frame data passed to managed layer callbacks.
/// </summary>
struct VideoFrameData
{
    void* pTexture;         // Pointer to ID3D11Texture2D
    LONGLONG timestamp;     // Timestamp in 100ns ticks
    UINT32 width;           // Frame width in pixels
    UINT32 height;          // Frame height in pixels
};

/// <summary>
/// Audio sample data passed to managed layer callbacks.
/// </summary>
struct AudioSampleData
{
    const BYTE* pData;      // Pointer to audio sample data
    UINT32 numFrames;       // Number of audio frames
    LONGLONG timestamp;     // Timestamp in 100ns ticks
    UINT32 sampleRate;      // Sample rate in Hz
    UINT16 channels;        // Number of channels
    UINT16 bitsPerSample;   // Bits per sample
};

// Callback function pointer types for managed layer
using VideoFrameCallback = void(__stdcall*)(const VideoFrameData* pFrameData);
using AudioSampleCallback = void(__stdcall*)(const AudioSampleData* pSampleData);
