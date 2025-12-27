#pragma once
#include "Result.h"
#include <wil/com.h>
#include <span>
#include <cstdint>

// Forward declarations
struct IMFSample;

/// <summary>
/// Creates Media Foundation samples from raw video or audio data.
/// </summary>
class SampleBuilder
{
public:
    SampleBuilder() = default;
    ~SampleBuilder() = default;

    SampleBuilder(const SampleBuilder&) = delete;
    SampleBuilder& operator=(const SampleBuilder&) = delete;

    Result<wil::com_ptr<IMFSample>> CreateVideoSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const;

    Result<wil::com_ptr<IMFSample>> CreateAudioSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const;

private:
    Result<wil::com_ptr<IMFSample>> CreateSampleFromData(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration,
        const char* context) const;
};
