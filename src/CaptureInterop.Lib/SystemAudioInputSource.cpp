#include "pch.h"
#include "SystemAudioInputSource.h"

SystemAudioInputSource::SystemAudioInputSource()
    : m_handler(std::make_unique<AudioCaptureHandler>())
{
}

SystemAudioInputSource::~SystemAudioInputSource()
{
    Stop();
}

bool SystemAudioInputSource::Initialize(HRESULT* outHr)
{
    // Initialize with loopback mode (true) for system audio capture
    return m_handler->Initialize(true, outHr);
}

bool SystemAudioInputSource::Start(HRESULT* outHr)
{
    return m_handler->Start(outHr);
}

void SystemAudioInputSource::Stop()
{
    m_handler->Stop();
}

WAVEFORMATEX* SystemAudioInputSource::GetFormat() const
{
    return m_handler->GetFormat();
}

void SystemAudioInputSource::SetSinkWriter(MP4SinkWriter* sinkWriter)
{
    m_handler->SetSinkWriter(sinkWriter);
}

void SystemAudioInputSource::SetEnabled(bool enabled)
{
    m_handler->SetEnabled(enabled);
}

bool SystemAudioInputSource::IsEnabled() const
{
    return m_handler->IsEnabled();
}

bool SystemAudioInputSource::IsRunning() const
{
    return m_handler->IsRunning();
}
