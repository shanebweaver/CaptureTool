#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/OwnedAudioSampleBuilder.h"

#include <cstring>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace CaptureInterop::V2;
using namespace CaptureInterop::V2::Audio;

namespace
{
    AudioMediaType CreateMediaType()
    {
        return AudioMediaType{
            48000,
            2,
            16,
            4,
            AudioSampleFormat::Pcm16
        };
    }

    AudioMediaType CreateFloatMediaType()
    {
        return AudioMediaType{
            48000,
            1,
            32,
            4,
            AudioSampleFormat::Float32
        };
    }

    WasapiAudioPacketView CreatePacketView(const uint8_t* data, uint32_t frameCount = 2)
    {
        return WasapiAudioPacketView{
            SourceId::FromValue(22),
            StreamId::FromValue(44),
            MediaTime::FromTicks(123),
            CreateMediaType(),
            data,
            frameCount,
            false,
            AudioSourceTimingMetadata{
                AudioTimestampSource::WasapiPacketPosition,
                777,
                888,
                false
            }
        };
    }
}

namespace CaptureInteropTests
{
    TEST_CLASS(V2OwnedAudioSampleBuilderTests)
    {
    public:
        TEST_METHOD(BorrowedPacketMemory_IsCopiedIntoOwnedSample)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4, 5, 6, 7, 8 };

            AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(CreatePacketView(borrowedPacket));
            borrowedPacket[0] = 99;
            borrowedPacket[7] = 88;

            Assert::AreEqual(static_cast<size_t>(8), sample.pcmData.size());
            Assert::AreEqual(1u, static_cast<uint32_t>(sample.pcmData[0]));
            Assert::AreEqual(8u, static_cast<uint32_t>(sample.pcmData[7]));
        }

        TEST_METHOD(SilentPacket_ProducesOwnedZeroedBuffer)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4 };
            WasapiAudioPacketView packet = CreatePacketView(borrowedPacket, 1);
            packet.silent = true;

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(packet);

            Assert::AreEqual(static_cast<size_t>(4), sample.pcmData.size());
            for (const uint8_t value : sample.pcmData)
            {
                Assert::AreEqual(0u, static_cast<uint32_t>(value));
            }
            Assert::IsTrue(sample.sourceTiming.silent);
            Assert::IsFalse(sample.sourceTiming.synthesizedSilence);
        }

        TEST_METHOD(SilentPacket_Float32SamplesAreZero)
        {
            float borrowedPacket[] = { 0.5f };
            WasapiAudioPacketView packet = CreatePacketView(
                reinterpret_cast<const uint8_t*>(borrowedPacket),
                1);
            packet.mediaType = CreateFloatMediaType();
            packet.silent = true;

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(packet);

            float observed = 1.0f;
            std::memcpy(&observed, sample.pcmData.data(), sizeof(observed));
            Assert::AreEqual(0.0f, observed);
        }

        TEST_METHOD(SilentSamples_DoNotReuseMutableRetainedBuffers)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4 };
            WasapiAudioPacketView packet = CreatePacketView(borrowedPacket, 1);
            packet.silent = true;

            AudioSample first = BuildOwnedAudioSampleFromWasapiPacket(packet);
            AudioSample second = BuildOwnedAudioSampleFromWasapiPacket(packet);
            first.pcmData[0] = 99;

            Assert::AreEqual(99u, static_cast<uint32_t>(first.pcmData[0]));
            Assert::AreEqual(0u, static_cast<uint32_t>(second.pcmData[0]));
        }

        TEST_METHOD(SynthesizedSilence_IsBoundedAndMarked)
        {
            const AudioSample sample = BuildBoundedSynthesizedSilentAudioSample(
                SourceId::FromValue(22),
                StreamId::FromValue(44),
                MediaTime::FromTicks(123),
                CreateMediaType(),
                96000);

            Assert::AreEqual(48000u, sample.frameCount);
            Assert::AreEqual(10'000'000ll, sample.duration.ticks100ns);
            Assert::IsTrue(sample.sourceTiming.silent);
            Assert::IsTrue(sample.sourceTiming.synthesizedSilence);
            Assert::AreEqual(
                static_cast<int>(AudioTimestampSource::GeneratedContinuity),
                static_cast<int>(sample.sourceTiming.timestampSource));
        }

        TEST_METHOD(FrameCountAndDuration_ArePreserved)
        {
            uint8_t borrowedPacket[12]{};
            WasapiAudioPacketView packet = CreatePacketView(borrowedPacket, 3);

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(packet);

            Assert::AreEqual(3u, sample.frameCount);
            Assert::AreEqual(625ll, sample.duration.ticks100ns);
            Assert::AreEqual(static_cast<size_t>(12), sample.pcmData.size());
        }

        TEST_METHOD(Metadata_IsCopiedForSampleLifetime)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4 };
            WasapiAudioPacketView packet = CreatePacketView(borrowedPacket, 1);

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(packet);
            packet.sourceId = SourceId::FromValue(99);
            packet.streamId = StreamId::FromValue(100);
            packet.mediaType.sampleRate = 96000;

            Assert::AreEqual(22u, sample.sourceId.value);
            Assert::AreEqual(44u, sample.streamId.value);
            Assert::AreEqual(48000u, sample.mediaType.sampleRate);
        }

        TEST_METHOD(EmptyPacket_ProducesEmptyOwnedBuffer)
        {
            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(CreatePacketView(nullptr, 0));

            Assert::AreEqual(0u, sample.frameCount);
            Assert::IsTrue(sample.duration.IsZero());
            Assert::IsTrue(sample.pcmData.empty());
        }

        TEST_METHOD(WasapiPacketPositionTimestamp_MapsToSourceTimingMetadata)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4 };

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(CreatePacketView(borrowedPacket, 1));

            Assert::AreEqual(
                static_cast<int>(AudioTimestampSource::WasapiPacketPosition),
                static_cast<int>(sample.sourceTiming.timestampSource));
            Assert::AreEqual(777ull, sample.sourceTiming.packetPosition);
            Assert::AreEqual(888ull, sample.sourceTiming.qpcPosition);
            Assert::IsFalse(sample.sourceTiming.discontinuity);
        }

        TEST_METHOD(QpcTimestampAndDiscontinuity_AreCopiedToSourceTimingMetadata)
        {
            uint8_t borrowedPacket[] = { 1, 2, 3, 4 };
            WasapiAudioPacketView packet = CreatePacketView(borrowedPacket, 1);
            packet.sourceTiming.timestampSource = AudioTimestampSource::WasapiQpcPosition;
            packet.sourceTiming.packetPosition = 0;
            packet.sourceTiming.qpcPosition = 999;
            packet.sourceTiming.discontinuity = true;

            const AudioSample sample = BuildOwnedAudioSampleFromWasapiPacket(packet);

            Assert::AreEqual(
                static_cast<int>(AudioTimestampSource::WasapiQpcPosition),
                static_cast<int>(sample.sourceTiming.timestampSource));
            Assert::AreEqual(0ull, sample.sourceTiming.packetPosition);
            Assert::AreEqual(999ull, sample.sourceTiming.qpcPosition);
            Assert::IsTrue(sample.sourceTiming.discontinuity);
        }
    };
}
