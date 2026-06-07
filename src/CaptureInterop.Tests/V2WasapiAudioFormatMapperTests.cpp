#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/WasapiAudioFormatMapper.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Audio;

namespace
{
    WAVEFORMATEX CreateWaveFormat(
        WORD formatTag,
        WORD channels,
        DWORD sampleRate,
        WORD bitsPerSample)
    {
        WAVEFORMATEX format{};
        format.wFormatTag = formatTag;
        format.nChannels = channels;
        format.nSamplesPerSec = sampleRate;
        format.wBitsPerSample = bitsPerSample;
        format.nBlockAlign = static_cast<WORD>(channels * bitsPerSample / 8);
        format.nAvgBytesPerSec = sampleRate * format.nBlockAlign;
        return format;
    }

    WAVEFORMATEXTENSIBLE CreateExtensibleFormat(
        GUID subFormat,
        WORD channels,
        DWORD sampleRate,
        WORD bitsPerSample,
        WORD validBitsPerSample,
        DWORD channelMask)
    {
        WAVEFORMATEXTENSIBLE format{};
        format.Format = CreateWaveFormat(WAVE_FORMAT_EXTENSIBLE, channels, sampleRate, bitsPerSample);
        format.Format.cbSize = sizeof(WAVEFORMATEXTENSIBLE) - sizeof(WAVEFORMATEX);
        format.Samples.wValidBitsPerSample = validBitsPerSample;
        format.dwChannelMask = channelMask;
        format.SubFormat = subFormat;
        return format;
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2WasapiAudioFormatMapperTests)
    {
    public:
        TEST_METHOD(Pcm16_MapsToExplicitV2MediaType)
        {
            const WAVEFORMATEX format = CreateWaveFormat(WAVE_FORMAT_PCM, 1, 44100, 16);

            const WasapiAudioFormatMappingResult result = MapWasapiMixFormatToAudioMediaType(format);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(44100u, result.mediaType.sampleRate);
            Assert::AreEqual(1u, static_cast<uint32_t>(result.mediaType.channels));
            Assert::AreEqual(16u, static_cast<uint32_t>(result.mediaType.bitsPerSample));
            Assert::AreEqual(2u, static_cast<uint32_t>(result.mediaType.blockAlign));
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Pcm16),
                static_cast<int>(result.mediaType.sampleFormat));
        }

        TEST_METHOD(ExtensiblePcm24In32_PreservesContainerAndValidBits)
        {
            const WAVEFORMATEXTENSIBLE format = CreateExtensibleFormat(
                KSDATAFORMAT_SUBTYPE_PCM,
                2,
                96000,
                32,
                24,
                KSAUDIO_SPEAKER_STEREO);

            const WasapiAudioFormatMappingResult result =
                MapWasapiMixFormatToAudioMediaType(format.Format);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(96000u, result.mediaType.sampleRate);
            Assert::AreEqual(2u, static_cast<uint32_t>(result.mediaType.channels));
            Assert::AreEqual(32u, static_cast<uint32_t>(result.mediaType.bitsPerSample));
            Assert::AreEqual(8u, static_cast<uint32_t>(result.mediaType.blockAlign));
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Pcm24),
                static_cast<int>(result.mediaType.sampleFormat));
            Assert::IsTrue(result.diagnostics.channelMask.has_value());
            Assert::AreEqual(
                static_cast<uint32_t>(KSAUDIO_SPEAKER_STEREO),
                result.diagnostics.channelMask.value());
            Assert::IsTrue(result.diagnostics.validBitsPerSample.has_value());
            Assert::AreEqual(24u, static_cast<uint32_t>(result.diagnostics.validBitsPerSample.value()));
        }

        TEST_METHOD(Float32_MapsWithoutAssumingStereoOr48Khz)
        {
            const WAVEFORMATEX format = CreateWaveFormat(WAVE_FORMAT_IEEE_FLOAT, 6, 48000, 32);

            const WasapiAudioFormatMappingResult result = MapWasapiMixFormatToAudioMediaType(format);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(48000u, result.mediaType.sampleRate);
            Assert::AreEqual(6u, static_cast<uint32_t>(result.mediaType.channels));
            Assert::AreEqual(32u, static_cast<uint32_t>(result.mediaType.bitsPerSample));
            Assert::AreEqual(24u, static_cast<uint32_t>(result.mediaType.blockAlign));
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Float32),
                static_cast<int>(result.mediaType.sampleFormat));
        }

        TEST_METHOD(ExtensibleFloat32_MapsAndPreservesDiagnostics)
        {
            const WAVEFORMATEXTENSIBLE format = CreateExtensibleFormat(
                KSDATAFORMAT_SUBTYPE_IEEE_FLOAT,
                2,
                44100,
                32,
                32,
                KSAUDIO_SPEAKER_STEREO);

            const WasapiAudioFormatMappingResult result =
                MapWasapiMixFormatToAudioMediaType(format.Format);

            Assert::IsTrue(result.IsSuccess());
            Assert::AreEqual(
                static_cast<int>(AudioSampleFormat::Float32),
                static_cast<int>(result.mediaType.sampleFormat));
            Assert::AreEqual(
                static_cast<uint32_t>(KSAUDIO_SPEAKER_STEREO),
                result.diagnostics.channelMask.value());
            Assert::AreEqual(32u, static_cast<uint32_t>(result.diagnostics.validBitsPerSample.value()));
        }

        TEST_METHOD(UnsupportedFormat_ReturnsStructuredFailure)
        {
            const WAVEFORMATEX format = CreateWaveFormat(WAVE_FORMAT_ALAW, 2, 48000, 8);

            const WasapiAudioFormatMappingResult result = MapWasapiMixFormatToAudioMediaType(format);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.result.code));
            Assert::IsTrue(result.result.diagnostic.has_value());
            Assert::AreEqual("WasapiAudioFormatMapper", result.result.diagnostic->component.c_str());
            Assert::AreEqual("MapMixFormat", result.result.diagnostic->operation.c_str());
            Assert::IsTrue(result.result.diagnostic->nativeStatus.has_value());
            Assert::AreEqual(
                static_cast<int64_t>(WAVE_FORMAT_ALAW),
                result.result.diagnostic->nativeStatus.value());
        }

        TEST_METHOD(UnsupportedPcmPrecision_ReturnsStructuredFailure)
        {
            const WAVEFORMATEX format = CreateWaveFormat(WAVE_FORMAT_PCM, 2, 48000, 20);

            const WasapiAudioFormatMappingResult result = MapWasapiMixFormatToAudioMediaType(format);

            Assert::IsTrue(result.IsFailure());
            Assert::AreEqual(
                static_cast<uint32_t>(CoreResultCode::UnsupportedOperation),
                static_cast<uint32_t>(result.result.code));
        }
    };
}
