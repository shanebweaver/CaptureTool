#include "pch.h"
#include "AudioRoutingConfig.h"
#include <algorithm>

AudioRoutingConfig::AudioRoutingConfig()
    : m_mixedMode(false)
{
}

AudioRoutingConfig::~AudioRoutingConfig()
{
}

void AudioRoutingConfig::SetSourceTrack(uint64_t sourceId, int trackIndex)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    // Validate track index
    if (trackIndex < -1 || trackIndex > 5)
    {
        return;
    }

    if (m_sourceConfigs.find(sourceId) == m_sourceConfigs.end())
    {
        m_sourceConfigs[sourceId] = SourceConfig();
    }

    m_sourceConfigs[sourceId].trackIndex = trackIndex;
}

int AudioRoutingConfig::GetSourceTrack(uint64_t sourceId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_sourceConfigs.find(sourceId);
    if (it == m_sourceConfigs.end())
    {
        return -1;  // Auto mode
    }

    return it->second.trackIndex;
}

void AudioRoutingConfig::SetSourceVolume(uint64_t sourceId, float volume)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Clamp volume to valid range (0.0 - 2.0)
    volume = std::max(0.0f, std::min(2.0f, volume));

    if (m_sourceConfigs.find(sourceId) == m_sourceConfigs.end())
    {
        m_sourceConfigs[sourceId] = SourceConfig();
    }

    m_sourceConfigs[sourceId].volume = volume;
}

float AudioRoutingConfig::GetSourceVolume(uint64_t sourceId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_sourceConfigs.find(sourceId);
    if (it == m_sourceConfigs.end())
    {
        return 1.0f;  // Default volume
    }

    return it->second.volume;
}

void AudioRoutingConfig::SetSourceMuted(uint64_t sourceId, bool muted)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (m_sourceConfigs.find(sourceId) == m_sourceConfigs.end())
    {
        m_sourceConfigs[sourceId] = SourceConfig();
    }

    m_sourceConfigs[sourceId].muted = muted;
}

bool AudioRoutingConfig::IsSourceMuted(uint64_t sourceId) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_sourceConfigs.find(sourceId);
    if (it == m_sourceConfigs.end())
    {
        return false;  // Not muted by default
    }

    return it->second.muted;
}

void AudioRoutingConfig::SetTrackName(int trackIndex, const wchar_t* name)
{
    std::lock_guard<std::mutex> lock(m_mutex);

    // Validate track index
    if (trackIndex < 0 || trackIndex > 5)
    {
        return;
    }

    if (name != nullptr)
    {
        m_trackNames[trackIndex] = name;
    }
    else
    {
        m_trackNames.erase(trackIndex);
    }
}

const wchar_t* AudioRoutingConfig::GetTrackName(int trackIndex) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    auto it = m_trackNames.find(trackIndex);
    if (it == m_trackNames.end())
    {
        return nullptr;
    }

    return it->second.c_str();
}

bool AudioRoutingConfig::IsMixedMode() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_mixedMode;
}

void AudioRoutingConfig::SetMixedMode(bool mixed)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_mixedMode = mixed;

    // In mixed mode, all sources go to track 0
    if (mixed)
    {
        for (auto& pair : m_sourceConfigs)
        {
            pair.second.trackIndex = 0;
        }
    }
}

void AudioRoutingConfig::Clear()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    m_sourceConfigs.clear();
    m_trackNames.clear();
    m_mixedMode = false;
}

void AudioRoutingConfig::GetConfiguredSources(uint64_t* sourceIds, int* count, int maxCount) const
{
    std::lock_guard<std::mutex> lock(m_mutex);

    if (sourceIds == nullptr || count == nullptr)
    {
        return;
    }

    int idx = 0;
    for (const auto& pair : m_sourceConfigs)
    {
        if (idx >= maxCount)
        {
            break;
        }
        sourceIds[idx++] = pair.first;
    }

    *count = idx;
}
