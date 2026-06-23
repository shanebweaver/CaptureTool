#pragma once
#include <windows.h>
#include <cstdint>
#include "CallbackTypes.h"
#include "ScreenRecorder.h"

struct AudioRecordingOptions
{
    const wchar_t* outputPath;
    uint32_t captureAudio;
    const wchar_t* audioInputSourceId;
    uint32_t audioInputVolumePercentage;
};

extern "C"
{
    __declspec(dllexport) CaptureRecorderResult StartAudioRecording(const AudioRecordingOptions* options);
    __declspec(dllexport) CaptureRecorderResult PauseAudioRecording();
    __declspec(dllexport) CaptureRecorderResult ResumeAudioRecording();
    __declspec(dllexport) CaptureRecorderResult StopAudioRecording();
    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingEnabled(uint32_t enabled);
    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingInputSource(const wchar_t* sourceId);
    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingInputVolume(uint32_t volumePercentage);
    __declspec(dllexport) CaptureRecorderResult RegisterAudioRecordingSampleCallback(AudioSampleCallback callback);
}
