#pragma once
#include <map>
#include <string>
#include <mutex>

/// <summary>
/// Configuration for audio routing: which sources go to which tracks.
/// Supports both mixed mode (all sources to track 0) and separate track mode.
/// </summary>
class AudioRoutingConfig
{
public:
    AudioRoutingConfig();
    ~AudioRoutingConfig();

    /// <summary>
    /// Set the track index for a source.
    /// </summary>
    /// <param name="sourceId">Unique source identifier</param>
    /// <param name="trackIndex">Track index (-1 for auto, 0-5 for specific track)</param>
    void SetSourceTrack(uint64_t sourceId, int trackIndex);

    /// <summary>
    /// Get the track index for a source.
    /// Returns -1 if not configured (auto mode).
    /// </summary>
    int GetSourceTrack(uint64_t sourceId) const;

    /// <summary>
    /// Set volume for a source (0.0 - 2.0).
    /// </summary>
    void SetSourceVolume(uint64_t sourceId, float volume);

    /// <summary>
    /// Get volume for a source. Returns 1.0 if not configured.
    /// </summary>
    float GetSourceVolume(uint64_t sourceId) const;

    /// <summary>
    /// Set mute state for a source.
    /// </summary>
    void SetSourceMuted(uint64_t sourceId, bool muted);

    /// <summary>
    /// Check if source is muted. Returns false if not configured.
    /// </summary>
    bool IsSourceMuted(uint64_t sourceId) const;

    /// <summary>
    /// Set track name for metadata.
    /// </summary>
    void SetTrackName(int trackIndex, const wchar_t* name);

    /// <summary>
    /// Get track name. Returns nullptr if not set.
    /// </summary>
    const wchar_t* GetTrackName(int trackIndex) const;

    /// <summary>
    /// Check if using mixed mode (all sources to track 0).
    /// </summary>
    bool IsMixedMode() const;

    /// <summary>
    /// Set mixed mode. If true, all sources route to track 0.
    /// </summary>
    void SetMixedMode(bool mixed);

    /// <summary>
    /// Clear all configuration.
    /// </summary>
    void Clear();

    /// <summary>
    /// Get all configured source IDs.
    /// </summary>
    void GetConfiguredSources(uint64_t* sourceIds, int* count, int maxCount) const;

private:
    struct SourceConfig
    {
        int trackIndex = -1;    // -1 = auto
        float volume = 1.0f;
        bool muted = false;
    };

    mutable std::mutex m_mutex;
    std::map<uint64_t, SourceConfig> m_sourceConfigs;
    std::map<int, std::wstring> m_trackNames;
    bool m_mixedMode = false;
};
