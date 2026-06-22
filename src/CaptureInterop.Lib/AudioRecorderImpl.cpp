#include "pch.h"
#include "AudioRecorderImpl.h"
#include "SimpleMediaClockFactory.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsWaveSinkWriterFactory.h"

AudioRecorderImpl::AudioRecorderImpl(std::unique_ptr<WindowsAudioCaptureSessionFactory> factory)
    : m_factory(std::move(factory))
{
}

AudioRecorderImpl::AudioRecorderImpl()
    : AudioRecorderImpl(std::make_unique<WindowsAudioCaptureSessionFactory>(
        std::make_unique<SimpleMediaClockFactory>(),
        std::make_unique<WindowsLocalAudioCaptureSourceFactory>(),
        std::make_unique<WindowsWaveSinkWriterFactory>()))
{
}

AudioRecorderImpl::~AudioRecorderImpl()
{
    StopRecording();
}

bool AudioRecorderImpl::StartRecording(const AudioRecordingConfig& config, HRESULT* outHr)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_captureSession)
    {
        if (outHr) *outHr = E_NOT_VALID_STATE;
        return false;
    }

    auto session = m_factory->CreateSession(config);
    if (!session)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    if (m_audioSampleCallback)
    {
        session->SetAudioSampleCallback(m_audioSampleCallback);
    }

    HRESULT hr = S_OK;
    if (!session->Start(&hr))
    {
        if (outHr) *outHr = hr;
        return false;
    }

    m_captureSession = std::move(session);
    if (outHr) *outHr = S_OK;
    return true;
}

bool AudioRecorderImpl::PauseRecording()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_captureSession)
    {
        return false;
    }

    m_captureSession->Pause();
    return true;
}

bool AudioRecorderImpl::ResumeRecording()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_captureSession)
    {
        return false;
    }

    m_captureSession->Resume();
    return true;
}

bool AudioRecorderImpl::StopRecording()
{
    std::unique_ptr<WindowsAudioCaptureSession> session;
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        if (!m_captureSession)
        {
            return false;
        }

        session = std::move(m_captureSession);
    }

    session->Stop();
    return true;
}

bool AudioRecorderImpl::SetAudioCaptureEnabled(bool enabled)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_captureSession)
    {
        return false;
    }

    m_captureSession->ToggleAudioCapture(enabled);
    return true;
}

bool AudioRecorderImpl::SetAudioInputSource(const wchar_t* sourceId)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_captureSession)
    {
        return false;
    }

    return m_captureSession->SetAudioInputSource(sourceId);
}

bool AudioRecorderImpl::SetAudioInputVolume(uint32_t volumePercentage)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    if (!m_captureSession)
    {
        return false;
    }

    m_captureSession->SetAudioInputVolume(volumePercentage);
    return true;
}

void AudioRecorderImpl::SetAudioSampleCallback(AudioSampleCallback callback)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_audioSampleCallback = callback;
    if (m_captureSession)
    {
        m_captureSession->SetAudioSampleCallback(callback);
    }
}
