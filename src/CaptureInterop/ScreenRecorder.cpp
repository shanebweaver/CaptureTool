#include "ScreenRecorder.h"
#include "ScreenRecorderImpl.h"
#include <Windows.h>

namespace
{
    ScreenRecorderImpl& GetRecorder()
    {
        static ScreenRecorderImpl recorder;
        return recorder;
    }
}

// Exported API
extern "C"
{
    __declspec(dllexport) bool TryStartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool captureAudio) noexcept
    {
        try
        {
            return GetRecorder().StartRecording(hMonitor, outputPath, captureAudio);
        }
        catch (...)
        {
            return false;
        }
    }

    __declspec(dllexport) int32_t TryStartRecordingRegion(
        HMONITOR hMonitor,
        const wchar_t* outputPath,
        int32_t captureX,
        int32_t captureY,
        int32_t captureWidth,
        int32_t captureHeight,
        int32_t captureAudio) noexcept
    {
        try
        {
            if (captureX < 0 ||
                captureY < 0 ||
                captureWidth <= 0 ||
                captureHeight <= 0 ||
                captureX > INT32_MAX - captureWidth ||
                captureY > INT32_MAX - captureHeight)
            {
                return 0;
            }

            RECT captureArea
            {
                captureX,
                captureY,
                captureX + captureWidth,
                captureY + captureHeight
            };
            return GetRecorder().StartRecording(
                hMonitor,
                outputPath,
                captureAudio != 0,
                captureArea) ? 1 : 0;
        }
        catch (...)
        {
            return 0;
        }
    }

    __declspec(dllexport) void TryPauseRecording() noexcept
    {
        try
        {
            GetRecorder().PauseRecording();
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void TryResumeRecording() noexcept
    {
        try
        {
            GetRecorder().ResumeRecording();
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) HRESULT TryStopRecording(CaptureOperationStage* outStage) noexcept
    {
        if (outStage)
        {
            *outStage = CaptureOperationStage::None;
        }

        try
        {
            CaptureOperationResult result = GetRecorder().StopRecording();
            if (outStage)
            {
                *outStage = result.stage;
            }
            return result.hr;
        }
        catch (...)
        {
            if (outStage)
            {
                *outStage = CaptureOperationStage::NativeException;
            }
            return E_FAIL;
        }
    }

    __declspec(dllexport) void TryToggleAudioCapture(bool enabled) noexcept
    {
        try
        {
            GetRecorder().ToggleAudioCapture(enabled);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void SetVideoFrameCallback(VideoFrameCallback callback) noexcept
    {
        try
        {
            GetRecorder().SetVideoFrameCallback(callback);
        }
        catch (...)
        {
        }
    }

    __declspec(dllexport) void SetAudioSampleCallback(AudioSampleCallback callback) noexcept
    {
        try
        {
            GetRecorder().SetAudioSampleCallback(callback);
        }
        catch (...)
        {
        }
    }
}
