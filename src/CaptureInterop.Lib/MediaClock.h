#pragma once
#include <cstdint>
#include <mutex>

/// <summary>
/// Sample-driven master clock for media timing.
/// 
/// This clock provides the authoritative, monotonically-increasing timeline for all media sources
/// based on audio sample counts. The core invariant is:
///     CurrentTime() = startTime + (samples / sampleRate)
/// 
/// OWNERSHIP AND ACCESS:
/// - Only AudioCaptureHandler should call Advance() - audio is the law
/// - All other media sources (video, etc.) should use read-only access via GetMediaClock()
/// - The clock is created on AudioCaptureHandler::Start() and destroyed on Stop()
/// 
/// THREAD SAFETY:
/// - All methods are thread-safe via internal mutex
/// - Multiple threads can safely read CurrentTime() while audio thread advances the clock
/// 
/// FUTURE WORK:
/// - ClockMapper will use this as the reference timeline for timestamp translation
/// - Video capture will query this clock for synchronization
/// </summary>
class MediaClock {
public:
    using Ticks = int64_t;

    struct SampleRate { uint32_t Hz; };
    struct MediaTime { Ticks ticks = 0; };

    /// <summary>
    /// Construct a MediaClock with the given sample rate and start time.
    /// </summary>
    /// <param name="rate">Sample rate in Hz (e.g., 48000 for 48kHz audio)</param>
    /// <param name="start">Starting time in 100ns ticks (default: 0)</param>
    MediaClock(SampleRate rate, MediaTime start = {0})
        : rate_(rate), startTime_(start), samples_(0) {}

    /// <summary>
    /// Advance the clock by the given number of samples.
    /// 
    /// IMPORTANT: Only AudioCaptureHandler should call this!
    /// This method should be called once per audio buffer, immediately after
    /// reading samples from the audio device.
    /// </summary>
    /// <param name="numSamples">Number of audio samples (frames) to advance</param>
    void Advance(uint32_t numSamples) {
        std::lock_guard<std::mutex> lock(mutex_);
        samples_ += numSamples;
    }

    /// <summary>
    /// Get the current media time.
    /// 
    /// This is thread-safe and can be called by any thread at any time.
    /// The time is calculated as: startTime + (samples / sampleRate)
    /// Time units are 100ns ticks (Media Foundation standard).
    /// </summary>
    /// <returns>Current media time in 100ns ticks</returns>
    MediaTime CurrentTime() const {
        std::lock_guard<std::mutex> lock(mutex_);
        // Convert samples to 100ns ticks
        // 10'000'000 = 100ns ticks per second
        Ticks additionalTicks = (static_cast<Ticks>(samples_) * 10'000'000) / rate_.Hz;
        return { startTime_.ticks + additionalTicks };
    }

    /// <summary>
    /// Get the sample rate this clock is based on.
    /// </summary>
    /// <returns>Sample rate in Hz</returns>
    SampleRate GetSampleRate() const { return rate_; }

    /// <summary>
    /// Get the start time of this clock.
    /// </summary>
    /// <returns>Start time in 100ns ticks</returns>
    MediaTime GetStartTime() const { return startTime_; }

    /// <summary>
    /// Get the total number of samples advanced since clock creation.
    /// Useful for diagnostics and validation.
    /// </summary>
    /// <returns>Total samples captured</returns>
    int64_t GetTotalSamplesCaptured() const { 
        std::lock_guard<std::mutex> lock(mutex_);
        return samples_;
    }

private:
    SampleRate rate_;              // Sample rate in Hz
    MediaTime startTime_;          // Starting time offset in 100ns ticks
    mutable std::mutex mutex_;     // Protects samples_ for thread safety
    int64_t samples_;              // Total samples advanced after start
};
