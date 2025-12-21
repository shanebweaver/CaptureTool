#include "pch.h"
#include "WasapiAudioCaptureSource.h"

WasapiAudioCaptureSource::WasapiAudioCaptureSource()
    : m_handler(std::make_unique<AudioCaptureHandler>())
{
}

WasapiAudioCaptureSource::~WasapiAudioCaptureSource()
{
    Stop();
}

bool WasapiAudioCaptureSource::Initialize(HRESULT* outHr)
{
    // Initialize with loopback mode (true) for system audio capture
    return m_handler->Initialize(true, outHr);
}

bool WasapiAudioCaptureSource::Start(HRESULT* outHr)
{
    return m_handler->Start(outHr);
}

void WasapiAudioCaptureSource::Stop()
{
    m_handler->Stop();
}

WAVEFORMATEX* WasapiAudioCaptureSource::GetFormat() const
{
    return m_handler->GetFormat();
}

void WasapiAudioCaptureSource::SetSinkWriter(MP4SinkWriter* sinkWriter)
{
    m_handler->SetSinkWriter(sinkWriter);
}

void WasapiAudioCaptureSource::SetEnabled(bool enabled)
{
    m_handler->SetEnabled(enabled);
}

bool WasapiAudioCaptureSource::IsEnabled() const
{
    return m_handler->IsEnabled();
}

bool WasapiAudioCaptureSource::IsRunning() const
{
    return m_handler->IsRunning();
}
