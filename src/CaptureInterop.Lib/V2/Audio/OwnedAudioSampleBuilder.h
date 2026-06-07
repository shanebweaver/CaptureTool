#pragma once

#include "V2/Core/MediaSamples.h"

#include <algorithm>

namespace CaptureInterop::V2::Audio
{
    struct WasapiAudioPacketView
    {
        SourceId sourceId;
        StreamId streamId;
        MediaTime timestamp;
        AudioMediaType mediaType;
        const uint8_t* data{ nullptr };
        uint32_t frameCount{ 0 };
        bool silent{ false };
        AudioSourceTimingMetadata sourceTiming;
    };

    [[nodiscard]] inline MediaDuration AudioDurationForFrames(
        uint32_t frameCount,
        const AudioMediaType& mediaType) noexcept
    {
        if (mediaType.sampleRate == 0)
        {
            return MediaDuration::Zero();
        }

        return MediaDuration::FromTicks(
            static_cast<int64_t>(
                (static_cast<uint64_t>(frameCount) * MediaTicksPerSecond) / mediaType.sampleRate));
    }

    [[nodiscard]] inline AudioSample BuildOwnedAudioSampleFromWasapiPacket(
        const WasapiAudioPacketView& packet)
    {
        AudioSample sample;
        sample.sourceId = packet.sourceId;
        sample.streamId = packet.streamId;
        sample.timestamp = packet.timestamp;
        sample.duration = AudioDurationForFrames(packet.frameCount, packet.mediaType);
        sample.frameCount = packet.frameCount;
        sample.mediaType = packet.mediaType;
        sample.sourceTiming = packet.sourceTiming;

        const size_t byteCount =
            static_cast<size_t>(packet.frameCount) * packet.mediaType.blockAlign;
        sample.pcmData.resize(byteCount);
        if (packet.silent || packet.data == nullptr)
        {
            std::fill(sample.pcmData.begin(), sample.pcmData.end(), uint8_t{ 0 });
            return sample;
        }

        std::copy(packet.data, packet.data + byteCount, sample.pcmData.begin());
        return sample;
    }
}
