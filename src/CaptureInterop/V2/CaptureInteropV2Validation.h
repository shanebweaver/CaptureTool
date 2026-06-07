#pragma once

#include "CaptureInteropV2Exports.h"

#include <cstddef>
#include <cstdint>

namespace CaptureInterop::V2::Api
{
    inline constexpr float MinAudioGainDb = -60.0F;
    inline constexpr float MaxAudioGainDb = 12.0F;

    template <typename T>
    [[nodiscard]] bool HasDtoHeader(const T& value) noexcept
    {
        return value.size >= (offsetof(T, version) + sizeof(value.version))
            && value.version == CtCaptureV2_DtoVersion;
    }

    template <typename T>
    [[nodiscard]] bool HasFullDtoSize(const T& value) noexcept
    {
        return value.size >= sizeof(T);
    }

    [[nodiscard]] inline bool IsValidBool(uint8_t value) noexcept
    {
        return value == 0 || value == 1;
    }

    [[nodiscard]] inline bool IsValidSourceKind(int32_t value) noexcept
    {
        return value == CtCaptureV2_SourceKind_Desktop
            || value == CtCaptureV2_SourceKind_SystemAudio;
    }

    [[nodiscard]] inline bool IsValidContainerFormat(int32_t value) noexcept
    {
        return value == CtCaptureV2_ContainerFormat_Mp4;
    }

    [[nodiscard]] inline bool IsValidVideoCodec(int32_t value) noexcept
    {
        return value == CtCaptureV2_VideoCodec_None
            || value == CtCaptureV2_VideoCodec_H264;
    }

    [[nodiscard]] inline bool IsValidAudioCodec(int32_t value) noexcept
    {
        return value == CtCaptureV2_AudioCodec_None
            || value == CtCaptureV2_AudioCodec_Aac;
    }

    [[nodiscard]] inline bool IsValidHdrPolicy(int32_t value) noexcept
    {
        return value == CtCaptureV2_HdrPolicy_Auto
            || value == CtCaptureV2_HdrPolicy_Preserve
            || value == CtCaptureV2_HdrPolicy_MapToSdr
            || value == CtCaptureV2_HdrPolicy_MatchDisplay
            || value == CtCaptureV2_HdrPolicy_ForceSdr;
    }

    [[nodiscard]] inline bool IsEmptyString(const char16_t* value) noexcept
    {
        return value == nullptr || value[0] == u'\0';
    }

    [[nodiscard]] inline int32_t ValidateDtoHeader(uint32_t size, uint32_t version) noexcept
    {
        if (size < sizeof(uint32_t) * 2)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        if (version != CtCaptureV2_DtoVersion)
        {
            return CtCaptureV2_ResultCode_UnsupportedVersion;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateVideoEncoding(const CtCaptureV2_VideoEncodingConfig& video) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(video.size, video.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(video)
            || video.reserved0 != 0
            || video.reserved1 != 0
            || !IsValidBool(video.hardwareAccelerationPreferred)
            || !IsValidVideoCodec(video.codec))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        if (video.codec == CtCaptureV2_VideoCodec_H264
            && (video.bitrate == 0 || video.frameRateNumerator == 0 || video.frameRateDenominator == 0))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateAudioEncoding(const CtCaptureV2_AudioEncodingConfig& audio) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(audio.size, audio.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(audio)
            || audio.reserved != 0
            || !IsValidAudioCodec(audio.codec))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        if (audio.codec == CtCaptureV2_AudioCodec_Aac
            && (audio.bitrate == 0 || audio.sampleRate == 0 || audio.channels == 0))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateOutputConfig(const CtCaptureV2_OutputConfig& output) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(output.size, output.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(output)
            || IsEmptyString(output.outputPath)
            || !IsValidContainerFormat(output.containerFormat))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        if (const int32_t result = ValidateVideoEncoding(output.video); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (const int32_t result = ValidateAudioEncoding(output.audio); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (output.video.codec == CtCaptureV2_VideoCodec_None
            && output.audio.codec == CtCaptureV2_AudioCodec_None)
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateSourceConfig(const CtCaptureV2_SourceConfig& source) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(source.size, source.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(source)
            || source.sourceId == 0
            || !IsValidSourceKind(source.sourceKind)
            || !IsValidBool(source.enabled)
            || source.reserved0 != 0
            || source.reserved1 != 0)
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        if (source.sourceKind == CtCaptureV2_SourceKind_Desktop
            && source.enabled != 0
            && (source.captureRect.width < 0 || source.captureRect.height < 0))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateToneMappingConfig(const CtCaptureV2_ToneMappingConfig& toneMapping) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(toneMapping.size, toneMapping.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(toneMapping)
            || !IsValidHdrPolicy(toneMapping.hdrPolicy)
            || !IsValidBool(toneMapping.preserveMetadataWhenPossible)
            || toneMapping.reserved0 != 0
            || toneMapping.reserved1 != 0)
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateAudioGainConfig(const CtCaptureV2_AudioGainConfig& gain) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(gain.size, gain.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(gain)
            || gain.sourceId == 0
            || gain.reserved != 0
            || gain.gainDb < MinAudioGainDb
            || gain.gainDb > MaxAudioGainDb)
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateControlConfig(const CtCaptureV2_ControlConfig& controls) noexcept
    {
        if (const int32_t result = ValidateDtoHeader(controls.size, controls.version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(controls)
            || !IsValidBool(controls.startMuted)
            || controls.reserved0 != 0
            || controls.reserved1 != 0
            || (controls.audioGainCount > 0 && controls.audioGains == nullptr)
            || (controls.audioGainCount == 0 && controls.audioGains != nullptr))
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        for (uint32_t index = 0; index < controls.audioGainCount; ++index)
        {
            if (const int32_t result = ValidateAudioGainConfig(controls.audioGains[index]); result != CtCaptureV2_ResultCode_Success)
            {
                return result;
            }
        }

        return CtCaptureV2_ResultCode_Success;
    }

    [[nodiscard]] inline int32_t ValidateConfig(const CtCaptureV2_Config* config) noexcept
    {
        if (config == nullptr)
        {
            return CtCaptureV2_ResultCode_InvalidArgument;
        }

        if (const int32_t result = ValidateDtoHeader(config->size, config->version); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (!HasFullDtoSize(*config)
            || config->reserved != 0
            || config->sourceCount == 0
            || config->sources == nullptr)
        {
            return CtCaptureV2_ResultCode_ValidationFailed;
        }

        for (uint32_t index = 0; index < config->sourceCount; ++index)
        {
            const CtCaptureV2_SourceConfig& source = config->sources[index];
            if (const int32_t result = ValidateSourceConfig(source); result != CtCaptureV2_ResultCode_Success)
            {
                return result;
            }

            for (uint32_t previous = 0; previous < index; ++previous)
            {
                if (config->sources[previous].sourceId == source.sourceId)
                {
                    return CtCaptureV2_ResultCode_ValidationFailed;
                }
            }
        }

        if (const int32_t result = ValidateOutputConfig(config->output); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        if (const int32_t result = ValidateToneMappingConfig(config->toneMapping); result != CtCaptureV2_ResultCode_Success)
        {
            return result;
        }

        return ValidateControlConfig(config->controls);
    }
}
