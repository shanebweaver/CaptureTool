#pragma once
#include "IStreamConfigurationBuilder.h"
#include "Result.h"
#include <wil/com.h>
#include <cstdint>
#include <mmreg.h>

// Forward declarations
struct IMFMediaType;

/// <summary>
/// Creates Media Foundation media types for H.264 video and AAC audio streams.
/// </summary>
class StreamConfigurationBuilder : public IStreamConfigurationBuilder
{
public:
    // Import nested types from interface
    using VideoConfig = IStreamConfigurationBuilder::VideoConfig;
    using AudioConfig = IStreamConfigurationBuilder::AudioConfig;

    StreamConfigurationBuilder() = default;
    ~StreamConfigurationBuilder() override = default;

    StreamConfigurationBuilder(const StreamConfigurationBuilder&) = delete;
    StreamConfigurationBuilder& operator=(const StreamConfigurationBuilder&) = delete;

    // IStreamConfigurationBuilder implementation
    Result<wil::com_ptr<IMFMediaType>> CreateVideoOutputType(const VideoConfig& config) const override;
    Result<wil::com_ptr<IMFMediaType>> CreateVideoInputType(const VideoConfig& config) const override;
    Result<wil::com_ptr<IMFMediaType>> CreateAudioOutputType(const AudioConfig& config) const override;
    Result<wil::com_ptr<IMFMediaType>> CreateAudioInputType(const AudioConfig& config) const override;

private:
    static constexpr uint32_t BYTES_PER_PIXEL_RGB32 = 4;
    static constexpr uint32_t AAC_OUTPUT_BITS_PER_SAMPLE = 16;
};
