#include "pch.h"
#include "WindowsLocalAudioCaptureSource.h"

WindowsLocalAudioCaptureSource::WindowsLocalAudioCaptureSource(IMediaClockReader* clockReader, std::wstring inputDeviceId)
    : m_handler(std::make_unique<AudioCaptureHandler>(clockReader))
    , m_inputDeviceId(std::move(inputDeviceId))
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
    bool useDefaultLoopbackSource = m_inputDeviceId.empty();
    return m_handler->Initialize(useDefaultLoopbackSource, m_inputDeviceId.c_str(), outHr);
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

void WindowsLocalAudioCaptureSource::SetVolume(uint32_t volumePercentage)
{
    m_handler->SetVolume(volumePercentage);
}

bool WindowsLocalAudioCaptureSource::IsRunning() const
{
    return m_handler->IsRunning();
}

bool WindowsLocalAudioCaptureSource::SetInputDeviceId(const wchar_t* sourceId, HRESULT* outHr)
{
    std::wstring newInputDeviceId = sourceId ? sourceId : L"";
    bool useDefaultLoopbackSource = newInputDeviceId.empty();

    if (!m_handler->SetInputDevice(useDefaultLoopbackSource, newInputDeviceId.c_str(), outHr))
    {
        return false;
    }

    m_inputDeviceId = std::move(newInputDeviceId);
    return true;
}

void WindowsLocalAudioCaptureSource::SetClockWriter(IMediaClockWriter* clockWriter)
{
    m_handler->SetClockWriter(clockWriter);
}
