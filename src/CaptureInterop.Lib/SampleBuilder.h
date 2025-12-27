#pragma once
#include "Result.h"
#include <wil/com.h>
#include <span>
#include <cstdint>

// Forward declarations
struct IMFSample;

/// <summary>
/// Builder for creating Media Foundation samples from raw data.
/// Encapsulates the process of creating IMFSample objects with proper timing information.
/// 
/// Implements RUST Principles:
/// - Principle #1 (Ownership): Returns com_ptr with clear ownership
/// - Principle #4 (Explicit Error Handling): Uses Result<T> for error handling
/// - Principle #7 (Const Correctness): Methods are const
/// </summary>
class SampleBuilder
{
public:
    SampleBuilder() = default;
    ~SampleBuilder() = default;

    // Delete copy operations
    SampleBuilder(const SampleBuilder&) = delete;
    SampleBuilder& operator=(const SampleBuilder&) = delete;

    /// <summary>
    /// Create a video sample from buffer data.
    /// </summary>
    /// <param name="data">Span of pixel data (RGB32 format).</param>
    /// <param name="timestamp">Sample timestamp in 100-nanosecond units.</param>
    /// <param name="duration">Sample duration in 100-nanosecond units.</param>
    /// <returns>Result containing IMFSample or error information.</returns>
    Result<wil::com_ptr<IMFSample>> CreateVideoSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const;

    /// <summary>
    /// Create an audio sample from buffer data.
    /// </summary>
    /// <param name="data">Span of audio data (PCM or Float format).</param>
    /// <param name="timestamp">Sample timestamp in 100-nanosecond units.</param>
    /// <param name="duration">Sample duration in 100-nanosecond units.</param>
    /// <returns>Result containing IMFSample or error information.</returns>
    Result<wil::com_ptr<IMFSample>> CreateAudioSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const;

private:
    /// <summary>
    /// Internal helper to create a sample from data.
    /// </summary>
    Result<wil::com_ptr<IMFSample>> CreateSampleFromData(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration,
        const char* context) const;
};
