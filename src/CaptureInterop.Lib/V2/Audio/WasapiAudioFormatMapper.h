#pragma once

#include "V2/Core/MediaTypes.h"
#include "V2/Core/ResultTypes.h"

#include <guiddef.h>
#include <ksmedia.h>
#include <mmreg.h>

#include <optional>

namespace CaptureInterop::V2::Audio
{
    struct WasapiAudioFormatDiagnostics
    {
        std::optional<uint32_t> channelMask;
        std::optional<uint16_t> validBitsPerSample;
    };

    struct WasapiAudioFormatMappingResult
    {
        AudioMediaType mediaType;
        WasapiAudioFormatDiagnostics diagnostics;
        OperationResult result;

        [[nodiscard]] bool IsSuccess() const noexcept
        {
            return result.IsSuccess();
        }

        [[nodiscard]] bool IsFailure() const noexcept
        {
            return result.IsFailure();
        }
    };

    namespace Detail
    {
        [[nodiscard]] inline OperationResult UnsupportedFormatResult(
            const char* message,
            uint16_t nativeFormatTag) noexcept
        {
            return OperationResult::Failure(
                CoreResultCode::UnsupportedOperation,
                "WasapiAudioFormatMapper",
                "MapMixFormat",
                message,
                nativeFormatTag);
        }

        [[nodiscard]] inline std::optional<AudioSampleFormat> MapPcmSampleFormat(
            uint16_t bitsPerSample,
            std::optional<uint16_t> validBitsPerSample) noexcept
        {
            const uint16_t precisionBits = validBitsPerSample.value_or(bitsPerSample);

            switch (precisionBits)
            {
            case 16:
                return AudioSampleFormat::Pcm16;
            case 24:
                return AudioSampleFormat::Pcm24;
            case 32:
                return AudioSampleFormat::Pcm32;
            default:
                return std::nullopt;
            }
        }
    }

    [[nodiscard]] inline WasapiAudioFormatMappingResult MapWasapiMixFormatToAudioMediaType(
        const WAVEFORMATEX& format) noexcept
    {
        WasapiAudioFormatMappingResult result;
        result.mediaType.sampleRate = format.nSamplesPerSec;
        result.mediaType.channels = format.nChannels;
        result.mediaType.bitsPerSample = format.wBitsPerSample;
        result.mediaType.blockAlign = format.nBlockAlign;

        uint16_t formatTag = format.wFormatTag;
        const GUID* extensibleSubFormat = nullptr;
        std::optional<uint16_t> validBitsPerSample;

        if (format.wFormatTag == WAVE_FORMAT_EXTENSIBLE)
        {
            if (format.cbSize < sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX))
            {
                result.result = Detail::UnsupportedFormatResult(
                    "Extensible WASAPI format is missing required format data",
                    format.wFormatTag);
                return result;
            }

            const auto& extensible = reinterpret_cast<const WAVEFORMATEXTENSIBLE&>(format);
            extensibleSubFormat = &extensible.SubFormat;
            formatTag = WAVE_FORMAT_EXTENSIBLE;
            validBitsPerSample = extensible.Samples.wValidBitsPerSample;
            result.diagnostics.channelMask = extensible.dwChannelMask;
            result.diagnostics.validBitsPerSample = validBitsPerSample;
        }

        const bool isPcm =
            format.wFormatTag == WAVE_FORMAT_PCM
            || (extensibleSubFormat != nullptr && InlineIsEqualGUID(*extensibleSubFormat, KSDATAFORMAT_SUBTYPE_PCM));
        const bool isFloat =
            format.wFormatTag == WAVE_FORMAT_IEEE_FLOAT
            || (extensibleSubFormat != nullptr && InlineIsEqualGUID(*extensibleSubFormat, KSDATAFORMAT_SUBTYPE_IEEE_FLOAT));

        if (isPcm)
        {
            const std::optional<AudioSampleFormat> sampleFormat =
                Detail::MapPcmSampleFormat(format.wBitsPerSample, validBitsPerSample);
            if (!sampleFormat.has_value())
            {
                result.result = Detail::UnsupportedFormatResult(
                    "PCM WASAPI format has unsupported sample precision",
                    formatTag);
                return result;
            }

            result.mediaType.sampleFormat = sampleFormat.value();
            result.result = OperationResult::Success();
            return result;
        }

        if (isFloat)
        {
            if (format.wBitsPerSample != 32)
            {
                result.result = Detail::UnsupportedFormatResult(
                    "IEEE float WASAPI format must be 32-bit float",
                    formatTag);
                return result;
            }

            result.mediaType.sampleFormat = AudioSampleFormat::Float32;
            result.result = OperationResult::Success();
            return result;
        }

        result.result = Detail::UnsupportedFormatResult(
            "WASAPI format tag or extensible subformat is not supported",
            formatTag);
        return result;
    }
}
