#include "pch.h"
#include "CppUnitTest.h"
#include "V2/Audio/OwnedAudioSampleBuilder.h"

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

    WasapiAudioPacketView CreatePacketView(const uint8_t* data, uint32_t frameCount = 2)
    {
        return WasapiAudioPacketView{
            SourceId::FromValue(22),
            StreamId::FromValue(44),
            MediaTime::FromTicks(123),
            CreateMediaType(),
            data,
            frameCount,
            false
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
    };
}
