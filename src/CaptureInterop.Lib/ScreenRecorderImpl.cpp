#include "pch.h"
#include "ScreenRecorderImpl.h"
#include "CaptureSession.h"

ScreenRecorderImpl::ScreenRecorderImpl()
    : m_captureSession(nullptr)
{
}

ScreenRecorderImpl::~ScreenRecorderImpl()
{
    StopRecording();
}

bool ScreenRecorderImpl::StartRecording(HMONITOR hMonitor, const wchar_t* outputPath, bool audioEnabled)
{
    // Stop any existing session
    StopRecording();

    // Create a new capture session
    m_captureSession = std::make_unique<CaptureSession>();
    
    // Start the session
    HRESULT hr = S_OK;
    if (!m_captureSession->Start(hMonitor, outputPath, audioEnabled, &hr))
    {
        m_captureSession.reset();
        return false;
    }

    return true;
}

void ScreenRecorderImpl::PauseRecording()
{
    // Not implemented yet
}

void ScreenRecorderImpl::ResumeRecording()
{
    // Not implemented yet
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
