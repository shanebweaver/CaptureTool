#include "ScreenRecorder.h"
#include "ScreenRecorderImpl.h"
#include <Windows.h>

static ScreenRecorderImpl g_recorder;

static CaptureRecorderResult RecorderResult(CaptureRecorderStatus status, HRESULT hr)
{
    return CaptureRecorderResult{ status, hr };
}

static CaptureRecorderResult Success()
{
    return RecorderResult(CaptureRecorderStatus::Success, S_OK);
}

static CaptureRecorderResult NoActiveSession()
{
    return RecorderResult(CaptureRecorderStatus::NoActiveSession, E_ILLEGAL_METHOD_CALL);
}

extern "C"
{
    __declspec(dllexport) CaptureRecorderResult StartScreenRecording(const CaptureRecordingOptions* options)
    {
        if (!options || !options->outputPath)
        {
            return RecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
        }

        CaptureSessionConfig config;
        switch (options->targetKind)
        {
        case CaptureRecordingTargetKind::Monitor:
            if (!options->hMonitor)
            {
                return RecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
            }
            config = CaptureSessionConfig::ForMonitor(
                options->hMonitor,
                options->outputPath,
                options->captureAudio != 0,
                options->frameRate,
                options->videoBitrate,
                options->audioBitrate,
                options->audioInputSourceId ? options->audioInputSourceId : L"",
                options->audioInputVolumePercentage);
            break;

        case CaptureRecordingTargetKind::Window:
            if (!options->hwnd)
            {
                return RecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
            }
            config = CaptureSessionConfig::ForWindow(
                options->hwnd,
                options->outputPath,
                options->captureAudio != 0,
                options->frameRate,
                options->videoBitrate,
                options->audioBitrate,
                options->audioInputSourceId ? options->audioInputSourceId : L"",
                options->audioInputVolumePercentage);
            break;

        case CaptureRecordingTargetKind::Rectangle:
            if (!options->hMonitor || options->width <= 0 || options->height <= 0)
            {
                return RecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
            }
            config = CaptureSessionConfig::ForRectangle(
                options->hMonitor,
                options->left,
                options->top,
                static_cast<uint32_t>(options->width),
                static_cast<uint32_t>(options->height),
                options->outputPath,
                options->captureAudio != 0,
                options->frameRate,
                options->videoBitrate,
                options->audioBitrate,
                options->audioInputSourceId ? options->audioInputSourceId : L"",
                options->audioInputVolumePercentage);
            break;

        default:
            return RecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
        }

        HRESULT hr = S_OK;
        if (!g_recorder.StartRecording(config, &hr))
        {
            return RecorderResult(hr == E_INVALIDARG ? CaptureRecorderStatus::InvalidArgument : CaptureRecorderStatus::StartFailed, hr);
        }

        return Success();
    }

    __declspec(dllexport) CaptureRecorderResult PauseScreenRecording()
    {
        return g_recorder.PauseRecording()
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult ResumeScreenRecording()
    {
        return g_recorder.ResumeRecording()
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult StopScreenRecording()
    {
        return g_recorder.StopRecording()
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioEnabled(uint32_t enabled)
    {
        return g_recorder.SetAudioCaptureEnabled(enabled != 0)
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioInputSource(const wchar_t* sourceId)
    {
        return g_recorder.SetAudioInputSource(sourceId ? sourceId : L"")
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetScreenRecordingAudioInputVolume(uint32_t volumePercentage)
    {
        return g_recorder.SetAudioInputVolume(volumePercentage)
            ? Success()
            : NoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult RegisterVideoFrameCallback(VideoFrameCallback callback)
    {
        g_recorder.SetVideoFrameCallback(callback);
        return Success();
    }

    __declspec(dllexport) CaptureRecorderResult RegisterAudioSampleCallback(AudioSampleCallback callback)
    {
        g_recorder.SetAudioSampleCallback(callback);
        return Success();
    }
}
