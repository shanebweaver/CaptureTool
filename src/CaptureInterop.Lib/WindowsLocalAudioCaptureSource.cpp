#include "pch.h"
#include "WindowsLocalAudioCaptureSource.h"

WindowsLocalAudioCaptureSource::WindowsLocalAudioCaptureSource(IMediaClockReader* clockReader)
    : m_handler(std::make_unique<AudioCaptureHandler>(clockReader))
{
    // Principle #6 (No Globals): Clock reader passed via constructor, not accessed globally
    // Principle #3 (No Nullable Pointers): Handler is always valid after construction
}

WindowsLocalAudioCaptureSource::~WindowsLocalAudioCaptureSource()
{
    Stop();
    // Principle #5 (RAII Everything): Stop() releases audio resources, then m_handler
    // is automatically cleaned up via std::unique_ptr destructor
}

bool WindowsLocalAudioCaptureSource::Initialize(HRESULT* outHr)
{
    // Initialize with loopback mode (true) for system audio capture
    return m_handler->Initialize(true, outHr);
}

bool WindowsLocalAudioCaptureSource::Start(HRESULT* outHr)
{
    return m_handler->Start(outHr);
}

void WindowsLocalAudioCaptureSource::Stop()
{
    m_handler->Stop();
}

WAVEFORMATEX* WindowsLocalAudioCaptureSource::GetFormat() const
{
    return m_handler->GetFormat();
}

void WindowsLocalAudioCaptureSource::SetAudioSampleReadyCallback(AudioSampleReadyCallback callback)
{
    m_handler->SetAudioSampleReadyCallback(callback);
}

void WindowsLocalAudioCaptureSource::SetEnabled(bool enabled)
{
    m_handler->SetEnabled(enabled);
}

bool WindowsLocalAudioCaptureSource::IsEnabled() const
{
    return m_handler->IsEnabled();
}

bool WindowsLocalAudioCaptureSource::IsRunning() const
{
    return m_handler->IsRunning();
}

void WindowsLocalAudioCaptureSource::SetClockWriter(IMediaClockWriter* clockWriter)
{
    m_handler->SetClockWriter(clockWriter);
}
