#include "pch.h"
#include "WindowsAudioCaptureSession.h"
#include "MediaTimeConstants.h"
#include <strsafe.h>
#include <cassert>

WindowsAudioCaptureSession::WindowsAudioCaptureSession(
    const AudioRecordingConfig& config,
    std::unique_ptr<IMediaClock> mediaClock,
    std::unique_ptr<IAudioCaptureSource> audioCaptureSource,
    std::unique_ptr<IWavSinkWriter> sinkWriter)
    : m_config(config)
    , m_mediaClock(std::move(mediaClock))
    , m_audioCaptureSource(std::move(audioCaptureSource))
    , m_sinkWriter(std::move(sinkWriter))
{
}

WindowsAudioCaptureSession::~WindowsAudioCaptureSession()
{
    Stop();
}

bool WindowsAudioCaptureSession::Initialize(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    if (m_stateMachine.GetState() != CaptureSessionState::Created)
    {
        if (m_stateMachine.GetState() == CaptureSessionState::Initialized)
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
        return false;
    }

    if (!m_config.IsValid() || !m_mediaClock || !m_audioCaptureSource || !m_sinkWriter)
    {
        m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }

    m_mediaClock->SetClockAdvancer(m_audioCaptureSource.get());

    if (!m_audioCaptureSource->Initialize(&hr))
    {
        m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        if (outHr) *outHr = hr;
        return false;
    }

    WAVEFORMATEX* audioFormat = m_audioCaptureSource->GetFormat();
    m_audioAvailable = audioFormat != nullptr;
    if (!m_audioAvailable)
    {
        m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    if (!m_sinkWriter->Initialize(m_config.outputPath.c_str(), audioFormat, &hr))
    {
        m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        if (outHr) *outHr = hr;
        return false;
    }

    SetupAudioCallback();
    m_stateMachine.TryTransitionTo(CaptureSessionState::Initialized);
    if (outHr) *outHr = S_OK;
    return true;
}

bool WindowsAudioCaptureSession::Start(HRESULT* outHr)
{
    HRESULT hr = S_OK;

    if (!m_stateMachine.CanTransitionTo(CaptureSessionState::Active))
    {
        if (m_stateMachine.GetState() == CaptureSessionState::Active)
        {
            if (outHr) *outHr = S_OK;
            return true;
        }

        if (outHr) *outHr = E_ILLEGAL_STATE_CHANGE;
        return false;
    }

    LARGE_INTEGER qpc{};
    QueryPerformanceCounter(&qpc);
    m_mediaClock->Start(qpc.QuadPart);

    m_audioCaptureSource->SetEnabled(m_config.audioEnabled);
    m_audioCaptureSource->SetVolume(m_config.audioInputVolumePercentage);

    if (!m_audioCaptureSource->Start(&hr))
    {
        m_stateMachine.TryTransitionTo(CaptureSessionState::Failed);
        if (outHr) *outHr = hr;
        return false;
    }

    m_stateMachine.TryTransitionTo(CaptureSessionState::Active);
    if (outHr) *outHr = S_OK;
    return true;
}

void WindowsAudioCaptureSession::Stop()
{
    CaptureSessionState state = m_stateMachine.GetState();
    if (m_cleanupCompleted || state == CaptureSessionState::Created || state == CaptureSessionState::Stopped)
    {
        return;
    }

    m_isShuttingDown.store(true, std::memory_order_release);
    m_audioCallbackHandle.Unregister();

    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->Stop();
        m_audioCaptureSource->SetAudioSampleReadyCallback(nullptr);
    }

    m_audioCallbackRegistry.Clear();

    if (m_mediaClock)
    {
        m_mediaClock->Pause();
    }

    if (m_sinkWriter)
    {
        m_sinkWriter->Finalize();
    }

    if (m_mediaClock)
    {
        m_mediaClock->Reset();
    }

    m_stateMachine.TryTransitionTo(CaptureSessionState::Stopped);
    m_isShuttingDown.store(false, std::memory_order_release);
    m_cleanupCompleted = true;
}

void WindowsAudioCaptureSession::Pause()
{
    if (m_stateMachine.GetState() == CaptureSessionState::Active && m_mediaClock)
    {
        m_mediaClock->Pause();
        m_stateMachine.TryTransitionTo(CaptureSessionState::Paused);
    }
}

void WindowsAudioCaptureSession::Resume()
{
    if (m_stateMachine.GetState() == CaptureSessionState::Paused && m_mediaClock)
    {
        m_mediaClock->Resume();
        m_stateMachine.TryTransitionTo(CaptureSessionState::Active);
    }
}

void WindowsAudioCaptureSession::ToggleAudioCapture(bool enabled)
{
    if (m_audioCaptureSource && m_audioCaptureSource->IsRunning())
    {
        m_audioCaptureSource->SetEnabled(enabled);
    }
}

bool WindowsAudioCaptureSession::SetAudioInputSource(const wchar_t* sourceId)
{
    if (!m_audioCaptureSource)
    {
        return false;
    }

    HRESULT hr = S_OK;
    bool wasEnabled = m_audioCaptureSource->IsEnabled();
    if (!m_audioCaptureSource->SetInputDeviceId(sourceId, &hr))
    {
        wchar_t message[256]{};
        StringCchPrintfW(
            message,
            ARRAYSIZE(message),
            L"[CaptureInterop Audio] Audio input source switch failed. HRESULT=0x%08X\r\n",
            static_cast<unsigned int>(hr));
        OutputDebugStringW(message);
        return false;
    }

    m_audioAvailable = m_audioCaptureSource->GetFormat() != nullptr;
    m_audioCaptureSource->SetEnabled(wasEnabled);
    return true;
}

void WindowsAudioCaptureSession::SetAudioInputVolume(uint32_t volumePercentage)
{
    if (m_audioCaptureSource)
    {
        m_audioCaptureSource->SetVolume(volumePercentage);
    }
}

void WindowsAudioCaptureSession::SetAudioSampleCallback(AudioSampleCallback callback)
{
    try
    {
        if (m_audioCallbackHandle.IsValid())
        {
            m_audioCallbackHandle.Unregister();
        }

        if (callback)
        {
            m_audioCallbackHandle = m_audioCallbackRegistry.Register([callback](const AudioSampleData& data) {
                callback(const_cast<AudioSampleData*>(&data));
            });
        }
    }
    catch (...)
    {
    }
}

void WindowsAudioCaptureSession::SetupAudioCallback()
{
    if (!m_audioAvailable)
    {
        return;
    }

    m_audioCaptureSource->SetAudioSampleReadyCallback(
        [this](const AudioSampleReadyEventArgs& args) {
            if (m_isShuttingDown.load(std::memory_order_acquire) ||
                m_stateMachine.GetState() != CaptureSessionState::Active)
            {
                return;
            }

            if (!args.pFormat || args.pFormat->nBlockAlign == 0 || args.pFormat->nSamplesPerSec == 0)
            {
                return;
            }

            HRESULT hr = m_sinkWriter->WriteAudioSample(args.data, args.timestamp);
            if (FAILED(hr))
            {
                m_audioCaptureSource->SetEnabled(false);
            }

            if (m_audioCallbackRegistry.HasCallbacks())
            {
                AudioSampleData sampleData{};
                sampleData.pData = args.data.data();
                sampleData.numFrames = static_cast<UINT32>(args.data.size()) / args.pFormat->nBlockAlign;
                sampleData.timestamp = args.timestamp;
                sampleData.sampleRate = args.pFormat->nSamplesPerSec;
                sampleData.channels = args.pFormat->nChannels;
                sampleData.bitsPerSample = args.pFormat->wBitsPerSample;

                m_audioCallbackRegistry.Invoke(sampleData);
            }
        });
}
