#include "pch.h"
#include "WindowsSystemAudioCaptureSource.h"

WindowsSystemAudioCaptureSource::WindowsSystemAudioCaptureSource()
    : m_handler(std::make_unique<AudioCaptureHandler>())
{
}

WindowsSystemAudioCaptureSource::~WindowsSystemAudioCaptureSource()
{
    Stop();
}

bool WindowsSystemAudioCaptureSource::Initialize(HRESULT* outHr)
{
    // Initialize with loopback mode (true) for system audio capture
    return m_handler->Initialize(true, outHr);
}

bool WindowsSystemAudioCaptureSource::Start(HRESULT* outHr)
{
    return m_handler->Start(outHr);
}

void WindowsSystemAudioCaptureSource::Stop()
{
    m_handler->Stop();
}

WAVEFORMATEX* WindowsSystemAudioCaptureSource::GetFormat() const
{
    return m_handler->GetFormat();
}

void WindowsSystemAudioCaptureSource::SetSinkWriter(MP4SinkWriter* sinkWriter)
{
    m_handler->SetSinkWriter(sinkWriter);
}

void WindowsSystemAudioCaptureSource::SetEnabled(bool enabled)
{
    m_handler->SetEnabled(enabled);
}

bool WindowsSystemAudioCaptureSource::IsEnabled() const
{
    return m_handler->IsEnabled();
}

bool WindowsSystemAudioCaptureSource::IsRunning() const
{
    return m_handler->IsRunning();
}
