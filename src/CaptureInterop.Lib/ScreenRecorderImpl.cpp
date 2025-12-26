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

bool ScreenRecorderImpl::StartRecording(const CaptureSessionConfig& config)
{
    // Stop any existing session
    StopRecording();

    // Create a new capture session using the factory with the config
    m_captureSession = m_factory->CreateSession(config);

    // Start the session
    HRESULT hr = S_OK;
    if (!m_captureSession->Start(&hr))
    {
        m_captureSession.reset();
        return false;
    }

    return true;
}

bool ScreenRecorderImpl::StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool audioEnabled)
{
    // Create config and delegate to config-based method
    CaptureSessionConfig config(hMonitor, outputPath, audioEnabled);
    return StartRecording(config);
}

void ScreenRecorderImpl::PauseRecording()
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession() && m_captureSession->IsActive())
    {
        m_captureSession->Pause();
    }
}

void ScreenRecorderImpl::ResumeRecording()
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession() && m_captureSession->IsActive())
    {
        m_captureSession->Resume();
    }
}

void ScreenRecorderImpl::StopRecording()
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession())
    {
        m_captureSession->Stop();
        m_captureSession.reset();
        // Principle #5 (RAII): Session cleanup happens automatically via unique_ptr
    }
}

void ScreenRecorderImpl::ToggleAudioCapture(bool enabled)
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession() && m_captureSession->IsActive())
    {
        m_captureSession->ToggleAudioCapture(enabled);
    }
}

void ScreenRecorderImpl::SetVideoFrameCallback(VideoFrameCallback callback)
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession())
    {
        m_captureSession->SetVideoFrameCallback(callback);
    }
}

void ScreenRecorderImpl::SetAudioSampleCallback(AudioSampleCallback callback)
{
    // Principle #3: Use HasActiveSession() for explicit null checking
    if (HasActiveSession())
    {
        m_captureSession->SetAudioSampleCallback(callback);
    }
}
