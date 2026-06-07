#pragma once

#include "V2/Core/MediaSamples.h"

#include <algorithm>

namespace CaptureInterop::V2::Audio
{
    constexpr uint32_t MaxSynthesizedSilenceDurationMs = 1000;

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

    [[nodiscard]] inline uint32_t BoundSynthesizedSilenceFrameCount(
        uint32_t requestedFrameCount,
        const AudioMediaType& mediaType) noexcept
    {
        if (mediaType.sampleRate == 0)
        {
            return 0;
        }

        const uint64_t maximumFrames =
            (static_cast<uint64_t>(mediaType.sampleRate) * MaxSynthesizedSilenceDurationMs) / 1000;
        return static_cast<uint32_t>(std::min<uint64_t>(requestedFrameCount, maximumFrames));
    }

    [[nodiscard]] inline AudioSample BuildOwnedSilentAudioSample(
        SourceId sourceId,
        StreamId streamId,
        MediaTime timestamp,
        const AudioMediaType& mediaType,
        uint32_t frameCount,
        AudioSourceTimingMetadata sourceTiming)
    {
        AudioSample sample;
        sample.sourceId = sourceId;
        sample.streamId = streamId;
        sample.timestamp = timestamp;
        sample.duration = AudioDurationForFrames(frameCount, mediaType);
        sample.frameCount = frameCount;
        sample.mediaType = mediaType;
        sample.sourceTiming = sourceTiming;
        sample.sourceTiming.silent = true;
        sample.pcmData.resize(static_cast<size_t>(frameCount) * mediaType.blockAlign);
        std::fill(sample.pcmData.begin(), sample.pcmData.end(), uint8_t{ 0 });
        return sample;
    }

    [[nodiscard]] inline AudioSample BuildBoundedSynthesizedSilentAudioSample(
        SourceId sourceId,
        StreamId streamId,
        MediaTime timestamp,
        const AudioMediaType& mediaType,
        uint32_t requestedFrameCount)
    {
        AudioSourceTimingMetadata sourceTiming;
        sourceTiming.timestampSource = AudioTimestampSource::GeneratedContinuity;
        sourceTiming.synthesizedSilence = true;

        return BuildOwnedSilentAudioSample(
            sourceId,
            streamId,
            timestamp,
            mediaType,
            BoundSynthesizedSilenceFrameCount(requestedFrameCount, mediaType),
            sourceTiming);
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

        if (packet.silent || packet.data == nullptr)
        {
            return BuildOwnedSilentAudioSample(
                packet.sourceId,
                packet.streamId,
                packet.timestamp,
                packet.mediaType,
                packet.frameCount,
                packet.sourceTiming);
        }

        const size_t byteCount =
            static_cast<size_t>(packet.frameCount) * packet.mediaType.blockAlign;
        sample.pcmData.resize(byteCount);
        std::copy(packet.data, packet.data + byteCount, sample.pcmData.begin());
        return sample;
    }
}
