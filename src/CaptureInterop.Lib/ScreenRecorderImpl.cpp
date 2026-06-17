#include "pch.h"
#include "ScreenRecorderImpl.h"
#include "WindowsGraphicsCaptureSessionFactory.h"
#include "WindowsLocalAudioCaptureSourceFactory.h"
#include "WindowsDesktopVideoCaptureSourceFactory.h"
#include "WindowsMFMP4SinkWriterFactory.h"
#include "SimpleMediaClockFactory.h"
#include "CaptureSessionConfig.h"
#include "ICaptureSessionFactory.h"

#include <strsafe.h>
#include <Windows.h>
#include <memory>
#include <utility>

ScreenRecorderImpl::ScreenRecorderImpl(std::unique_ptr<ICaptureSessionFactory> factory)
    : m_factory(std::move(factory))
    , m_captureSession(nullptr)
{
    // Principle #6 (No Globals): Factory is injected, not accessed as a singleton
    // Principle #3 (No Nullable Pointers): Session starts as nullptr, will be created on demand
}

ScreenRecorderImpl::ScreenRecorderImpl()
    : ScreenRecorderImpl(std::make_unique<WindowsGraphicsCaptureSessionFactory>(
        std::make_unique<SimpleMediaClockFactory>(),
        std::make_unique<WindowsLocalAudioCaptureSourceFactory>(),
        std::make_unique<WindowsDesktopVideoCaptureSourceFactory>(),
        std::make_unique<WindowsMFMP4SinkWriterFactory>()))
{
    // Default constructor creates standard factory chain
    // All dependencies are explicitly created and owned (Principle #6: No Globals)
}

ScreenRecorderImpl::~ScreenRecorderImpl()
{
    StopRecording();
    // Principle #5 (RAII Everything): Destructor ensures cleanup even if caller forgets
}

bool ScreenRecorderImpl::StartRecording(const CaptureSessionConfig& config, HRESULT* outHr)
{
    StopRecording();

    if (!config.IsValid())
    {
        if (outHr) *outHr = E_INVALIDARG;
        return false;
    }

    m_captureSession = m_factory->CreateSession(config);
    if (!m_captureSession)
    {
        if (outHr) *outHr = E_FAIL;
        return false;
    }

    if (m_videoFrameCallback)
    {
        m_captureSession->SetVideoFrameCallback(m_videoFrameCallback);
    }
    if (m_audioSampleCallback)
    {
        m_captureSession->SetAudioSampleCallback(m_audioSampleCallback);
    }

    HRESULT hr = S_OK;
    if (!m_captureSession->Start(&hr))
    {
        m_captureSession->Stop();
        m_captureSession.reset();
        if (outHr) *outHr = hr;
        return false;
    }

    if (outHr) *outHr = S_OK;
    return true;
}

bool ScreenRecorderImpl::StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool audioEnabled, HRESULT* outHr)
{
    CaptureSessionConfig config(hMonitor, outputPath, audioEnabled);
    return StartRecording(config, outHr);
}

bool ScreenRecorderImpl::PauseRecording()
{
    if (!HasActiveSession() || !m_captureSession->IsActive())
    {
        return false;
    }

    m_captureSession->Pause();
    return true;
}

bool ScreenRecorderImpl::ResumeRecording()
{
    if (!HasActiveSession() || !m_captureSession->IsActive())
    {
        return false;
    }

    m_captureSession->Resume();
    return true;
}

bool ScreenRecorderImpl::StopRecording()
{
    if (!HasActiveSession())
    {
        return false;
    }

    m_captureSession->Stop();
    m_captureSession.reset();
    return true;
}

bool ScreenRecorderImpl::SetAudioCaptureEnabled(bool enabled)
{
    if (!HasActiveSession() || !m_captureSession->IsActive())
    {
        return false;
    }

    m_captureSession->ToggleAudioCapture(enabled);
    return true;
}

bool ScreenRecorderImpl::SetAudioInputSource(const wchar_t* sourceId)
{
    if (!HasActiveSession() || !m_captureSession->IsActive())
    {
        return false;
    }

    return m_captureSession->SetAudioInputSource(sourceId ? sourceId : L"");
}

bool ScreenRecorderImpl::SetAudioInputVolume(uint32_t volumePercentage)
{
    if (!HasActiveSession() || !m_captureSession->IsActive())
    {
        return false;
    }

    m_captureSession->SetAudioInputVolume(volumePercentage);
    return true;
}

void ScreenRecorderImpl::SetVideoFrameCallback(VideoFrameCallback callback)
{
    // Store callback so it persists across session recreation
    m_videoFrameCallback = callback;
    
    // Also apply to current session if one exists
    if (HasActiveSession())
    {
        m_captureSession->SetVideoFrameCallback(callback);
    }
}

void ScreenRecorderImpl::SetAudioSampleCallback(AudioSampleCallback callback)
{
    // Store callback so it persists across session recreation
    m_audioSampleCallback = callback;
    
    // Also apply to current session if one exists
    if (HasActiveSession())
    {
        m_captureSession->SetAudioSampleCallback(callback);
    }
}
