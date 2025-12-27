#pragma once
#include "Result.h"
#include <wil/com.h>
#include <span>
#include <cstdint>

// Forward declarations
struct IMFSample;

/// <summary>
/// Interface for creating Media Foundation samples from raw data.
/// Provides abstraction for sample creation to enable dependency injection and testing.
/// </summary>
class ISampleBuilder
{
public:
    virtual ~ISampleBuilder() = default;

    /// <summary>
    /// Create a Media Foundation sample from video data.
    /// </summary>
    /// <param name="data">Span of raw video data.</param>
    /// <param name="timestamp">Timestamp in 100-nanosecond units.</param>
    /// <param name="duration">Duration in 100-nanosecond units.</param>
    /// <returns>Result containing the created IMFSample or error information.</returns>
    virtual Result<wil::com_ptr<IMFSample>> CreateVideoSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const = 0;

    /// <summary>
    /// Create a Media Foundation sample from audio data.
    /// </summary>
    /// <param name="data">Span of raw audio data.</param>
    /// <param name="timestamp">Timestamp in 100-nanosecond units.</param>
    /// <param name="duration">Duration in 100-nanosecond units.</param>
    /// <returns>Result containing the created IMFSample or error information.</returns>
    virtual Result<wil::com_ptr<IMFSample>> CreateAudioSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const = 0;
};
