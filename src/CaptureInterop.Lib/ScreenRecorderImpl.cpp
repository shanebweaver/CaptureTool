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
    (void)StopRecording();
    // Principle #5 (RAII Everything): Destructor ensures cleanup even if caller forgets
}

bool ScreenRecorderImpl::StartRecording(const CaptureSessionConfig& config)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Stop any existing session
    CaptureOperationResult stopResult = StopRecordingLocked();
    if (!stopResult.IsSuccess())
    {
        return false;
    }

    // Create a new capture session using the factory with the config
    m_captureSession = m_factory->CreateSession(config);
    if (!m_captureSession)
    {
        return false;
    }

    // Apply stored callbacks to the new session
    if (m_videoFrameCallback)
    {
        m_captureSession->SetVideoFrameCallback(m_videoFrameCallback);
    }
    if (m_audioSampleCallback)
    {
        m_captureSession->SetAudioSampleCallback(m_audioSampleCallback);
    }

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
    return StartRecording(hMonitor, outputPath, audioEnabled, {});
}

bool ScreenRecorderImpl::StartRecording(
    HMONITOR hMonitor,
    const wchar_t* outputPath,
    bool audioEnabled,
    RECT captureArea)
{
    CaptureSessionConfig config(
        hMonitor,
        outputPath,
        audioEnabled,
        30,
        5'000'000,
        128'000,
        captureArea);
    return StartRecording(config);
}

void ScreenRecorderImpl::PauseRecording()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->Pause();
    }
}

void ScreenRecorderImpl::ResumeRecording()
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->Resume();
    }
}

CaptureOperationResult ScreenRecorderImpl::StopRecording() noexcept
{
    try
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        return StopRecordingLocked();
    }
    catch (...)
    {
        return CaptureOperationResult::Failure(E_FAIL, CaptureOperationStage::NativeException);
    }
}

void ScreenRecorderImpl::ToggleAudioCapture(bool enabled)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_captureSession && m_captureSession->IsActive())
    {
        m_captureSession->ToggleAudioCapture(enabled);
    }
}

void ScreenRecorderImpl::SetVideoFrameCallback(VideoFrameCallback callback)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Store callback so it persists across session recreation
    m_videoFrameCallback = callback;
    
    // Also apply to current session if one exists
    if (m_captureSession)
    {
        m_captureSession->SetVideoFrameCallback(callback);
    }
}

void ScreenRecorderImpl::SetAudioSampleCallback(AudioSampleCallback callback)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Store callback so it persists across session recreation
    m_audioSampleCallback = callback;
    
    // Also apply to current session if one exists
    if (m_captureSession)
    {
        m_captureSession->SetAudioSampleCallback(callback);
    }
}

bool ScreenRecorderImpl::HasActiveSession() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_captureSession != nullptr;
}

CaptureOperationResult ScreenRecorderImpl::StopRecordingLocked() noexcept
{
    if (!m_captureSession)
    {
        return CaptureOperationResult::Success();
    }

    CaptureOperationResult result = m_captureSession->Stop();
    m_captureSession.reset();
    return result;
}
