#pragma once
#include "ISampleBuilder.h"
#include "Result.h"
#include <wil/com.h>
#include <span>
#include <cstdint>

// Forward declarations
struct IMFSample;

/// <summary>
/// Creates Media Foundation samples from raw video or audio data.
/// </summary>
class SampleBuilder : public ISampleBuilder
{
public:
    SampleBuilder() = default;
    ~SampleBuilder() override = default;

    SampleBuilder(const SampleBuilder&) = delete;
    SampleBuilder& operator=(const SampleBuilder&) = delete;

    // ISampleBuilder implementation
    Result<wil::com_ptr<IMFSample>> CreateVideoSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const override;

    Result<wil::com_ptr<IMFSample>> CreateVideoSampleFromBuffer(
        uint32_t bufferSize,
        int64_t timestamp,
        int64_t duration,
        const std::function<Result<void>(std::span<uint8_t>)>& fillBuffer) const override;

    Result<wil::com_ptr<IMFSample>> CreateAudioSample(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration) const override;

private:
    Result<wil::com_ptr<IMFSample>> CreateSampleFromData(
        std::span<const uint8_t> data,
        int64_t timestamp,
        int64_t duration,
        const char* context) const;

    Result<wil::com_ptr<IMFSample>> CreateSampleFromBuffer(
        uint32_t bufferSize,
        int64_t timestamp,
        int64_t duration,
        const char* context,
        const std::function<Result<void>(std::span<uint8_t>)>& fillBuffer) const;
};
