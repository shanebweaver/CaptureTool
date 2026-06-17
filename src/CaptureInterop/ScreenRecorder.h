#pragma once
#include <windows.h>
#include <cstdint>
#include "CallbackTypes.h"

enum class CaptureRecorderStatus : int32_t
{
    Success = 0,
    InvalidArgument = 1,
    InvalidState = 2,
    StartFailed = 3,
    NoActiveSession = 4
};

enum class CaptureRecordingTargetKind : int32_t
{
    Monitor = 0,
    Window = 1,
    Rectangle = 2
};

struct CaptureRecorderResult
{
    CaptureRecorderStatus status;
    HRESULT hresult;
};

struct CaptureRecordingOptions
{
    CaptureRecordingTargetKind targetKind;
    HMONITOR hMonitor;
    HWND hwnd;
    int32_t left;
    int32_t top;
    int32_t width;
    int32_t height;
    const wchar_t* outputPath;
    uint32_t captureAudio;
    uint32_t frameRate;
    uint32_t videoBitrate;
    uint32_t audioBitrate;
    const wchar_t* audioInputSourceId;
    uint32_t audioInputVolumePercentage;
};

extern "C"
{
    __declspec(dllexport) CaptureRecorderResult StartScreenRecording(const CaptureRecordingOptions* options);
    __declspec(dllexport) CaptureRecorderResult PauseScreenRecording();
    __declspec(dllexport) CaptureRecorderResult ResumeScreenRecording();
    __declspec(dllexport) CaptureRecorderResult StopScreenRecording();
    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioEnabled(uint32_t enabled);
    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioInputSource(const wchar_t* sourceId);
    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioInputVolume(uint32_t volumePercentage);

    __declspec(dllexport) CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback callback);
    __declspec(dllexport) CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback callback);
}
