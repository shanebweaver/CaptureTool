#pragma once
#include <cstdint>

/// <summary>
/// Write interface for advancing the MediaClock timeline.
/// Allows components (typically audio sources) to advance the clock based on media stream progress.
/// This interface is focused solely on time advancement operations.
/// </summary>
class IMediaClockWriter
{
public:
    virtual ~IMediaClockWriter() = default;

    /// <summary>
    /// Advance the clock based on audio sample timing.
    /// The clock tracks media time based on the number of audio frames processed.
    /// This is the primary method for keeping the media timeline synchronized with audio.
    /// </summary>
    /// <param name="numFrames">Number of audio frames in the current sample.</param>
    /// <param name="sampleRate">Sample rate of the audio stream in Hz.</param>
    virtual void AdvanceByAudioSamples(UINT32 numFrames, UINT32 sampleRate) = 0;
};
