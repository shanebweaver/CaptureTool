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
}

ScreenRecorderImpl::ScreenRecorderImpl()
    : ScreenRecorderImpl(std::make_unique<WindowsGraphicsCaptureSessionFactory>(
        new SimpleMediaClockFactory(),
        new WindowsLocalAudioCaptureSourceFactory(),
        new WindowsDesktopVideoCaptureSourceFactory(),
        new WindowsMFMP4SinkWriterFactory()))
{
}

ScreenRecorderImpl::~ScreenRecorderImpl()
{
    StopRecording();
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
    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->Pause();
    }
}

void ScreenRecorderImpl::ResumeRecording()
{
    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->Resume();
    }
}

void ScreenRecorderImpl::StopRecording()
{
    if (m_captureSession)
    {
        m_captureSession->Stop();
        m_captureSession.reset();
    }
}

void ScreenRecorderImpl::ToggleAudioCapture(bool enabled)
{
    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->ToggleAudioCapture(enabled);
    }
}

void ScreenRecorderImpl::SetVideoFrameCallback(VideoFrameCallback callback)
{
    if (m_captureSession)
    {
        m_captureSession->SetVideoFrameCallback(callback);
    }
}

void ScreenRecorderImpl::SetAudioSampleCallback(AudioSampleCallback callback)
{
    if (m_captureSession)
    {
        m_captureSession->SetAudioSampleCallback(callback);
    }
}
