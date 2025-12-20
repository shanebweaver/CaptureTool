#pragma once
#include "ICaptureSession.h"
#include "CaptureSessionConfig.h"
#include <memory>

/// <summary>
/// Factory interface for creating capture session instances.
/// Provides abstraction for session creation to enable dependency injection and testing.
/// </summary>
class ICaptureSessionFactory
{
public:
    virtual ~ICaptureSessionFactory() = default;

    /// <summary>
    /// Create a new capture session instance with configuration.
    /// </summary>
    /// <param name="config">Configuration settings for the capture session.</param>
    /// <returns>A unique pointer to a new ICaptureSession implementation.</returns>
    virtual std::unique_ptr<ICaptureSession> CreateSession(const CaptureSessionConfig& config) = 0;

    /// <summary>
    /// Create a new capture session instance.
    /// </summary>
    /// <returns>A unique pointer to a new ICaptureSession implementation.</returns>
    virtual std::unique_ptr<ICaptureSession> CreateSession() = 0;
};
