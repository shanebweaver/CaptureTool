#include "ScreenRecorder.h"
#include "ScreenRecorderImpl.h"
#include <Windows.h>

static ScreenRecorderImpl g_recorder;

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio)
    {
        return g_recorder.StartRecording(hMonitor, outputPath, captureAudio);
    }

    __declspec(dllexport) void TryPauseRecording()
    {
        g_recorder.PauseRecording();
    }

    __declspec(dllexport) void TryResumeRecording()
    {
        g_recorder.ResumeRecording();
    }

    __declspec(dllexport) void TryStopRecording()
    {
        g_recorder.StopRecording();
    }

    __declspec(dllexport) void TryToggleAudioCapture(bool enabled)
    {
        g_recorder.ToggleAudioCapture(enabled);
    }
}