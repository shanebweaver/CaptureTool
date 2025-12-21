#pragma once
#include "pch.h"
#include "ICaptureSessionFactory.h"

// Forward declarations
class IMediaClockFactory;
class IAudioCaptureSourceFactory;

/// <summary>
/// Factory implementation for creating Windows Graphics Capture sessions.
/// Creates WindowsGraphicsCaptureSession instances for screen recording.
/// </summary>
class WindowsGraphicsCaptureSessionFactory : public ICaptureSessionFactory
{
public:
    WindowsGraphicsCaptureSessionFactory(
        IMediaClockFactory* mediaClockFactory,
        IAudioCaptureSourceFactory* audioCaptureSourceFactory);
    ~WindowsGraphicsCaptureSessionFactory() override = default;

    // Delete copy and move operations
    WindowsGraphicsCaptureSessionFactory(const WindowsGraphicsCaptureSessionFactory&) = delete;
    WindowsGraphicsCaptureSessionFactory& operator=(const WindowsGraphicsCaptureSessionFactory&) = delete;
    WindowsGraphicsCaptureSessionFactory(WindowsGraphicsCaptureSessionFactory&&) = delete;
    WindowsGraphicsCaptureSessionFactory& operator=(WindowsGraphicsCaptureSessionFactory&&) = delete;

    /// <summary>
    /// Create a new Windows Graphics Capture session with configuration.
    /// </summary>
    /// <param name="config">Configuration settings for the capture session.</param>
    /// <returns>A unique pointer to a new WindowsGraphicsCaptureSession.</returns>
    std::unique_ptr<ICaptureSession> CreateSession(const CaptureSessionConfig& config) override;

private:
    IMediaClockFactory* m_mediaClockFactory;
    IAudioCaptureSourceFactory* m_audioCaptureSourceFactory;
};
