#include "AudioRecorder.h"
#include "AudioRecorderImpl.h"
#include <Windows.h>

static AudioRecorderImpl g_audioRecorder;

static CaptureRecorderResult AudioRecorderResult(CaptureRecorderStatus status, HRESULT hr)
{
    return CaptureRecorderResult{ status, hr };
}

static CaptureRecorderResult AudioSuccess()
{
    return AudioRecorderResult(CaptureRecorderStatus::Success, S_OK);
}

static CaptureRecorderResult AudioNoActiveSession()
{
    return AudioRecorderResult(CaptureRecorderStatus::NoActiveSession, E_ILLEGAL_METHOD_CALL);
}

extern "C"
{
    __declspec(dllexport) CaptureRecorderResult StartAudioRecording(const AudioRecordingOptions* options)
    {
        if (!options || !options->outputPath)
        {
            return AudioRecorderResult(CaptureRecorderStatus::InvalidArgument, E_INVALIDARG);
        }

        AudioRecordingConfig config(
            options->outputPath,
            options->captureAudio != 0,
            options->audioInputSourceId ? options->audioInputSourceId : L"",
            options->audioInputVolumePercentage);

        HRESULT hr = S_OK;
        if (!g_audioRecorder.StartRecording(config, &hr))
        {
            return AudioRecorderResult(hr == E_INVALIDARG ? CaptureRecorderStatus::InvalidArgument : CaptureRecorderStatus::StartFailed, hr);
        }

        return AudioSuccess();
    }

    __declspec(dllexport) CaptureRecorderResult PauseAudioRecording()
    {
        return g_audioRecorder.PauseRecording()
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult ResumeAudioRecording()
    {
        return g_audioRecorder.ResumeRecording()
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult StopAudioRecording()
    {
        return g_audioRecorder.StopRecording()
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingEnabled(uint32_t enabled)
    {
        return g_audioRecorder.SetAudioCaptureEnabled(enabled != 0)
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingInputSource(const wchar_t* sourceId)
    {
        return g_audioRecorder.SetAudioInputSource(sourceId ? sourceId : L"")
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult SetAudioRecordingInputVolume(uint32_t volumePercentage)
    {
        return g_audioRecorder.SetAudioInputVolume(volumePercentage)
            ? AudioSuccess()
            : AudioNoActiveSession();
    }

    __declspec(dllexport) CaptureRecorderResult RegisterAudioRecordingSampleCallback(AudioSampleCallback callback)
    {
        g_audioRecorder.SetAudioSampleCallback(callback);
        return AudioSuccess();
    }
}
