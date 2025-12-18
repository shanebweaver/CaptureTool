#include "pch.h"
#include "SourceManager.h"
#include <algorithm>

SourceManager& SourceManager::Instance()
{
    static SourceManager instance;
    return instance;
}

SourceManager::~SourceManager()
{
    Clear();
}

SourceHandle SourceManager::RegisterSource(IMediaSource* source)
{
    if (!source)
    {
        return INVALID_SOURCE_HANDLE;
    }
    
    std::lock_guard<std::mutex> lock(m_mutex);
    
    SourceHandle handle = GenerateHandle();
    
    SourceEntry entry;
    entry.source = source;
    entry.handle = handle;
    
    m_sources[handle] = entry;
    
    // AddRef since we're holding a reference
    source->AddRef();
    
    return handle;
}

void SourceManager::UnregisterSource(SourceHandle handle)
{
    IMediaSource* source = nullptr;
    
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        auto it = m_sources.find(handle);
        if (it == m_sources.end())
        {
            return;
        }
        
        source = it->second.source;
        m_sources.erase(it);
    }
    
    // Stop and release outside the lock to avoid deadlock
    if (source)
    {
        if (source->IsRunning())
        {
            source->Stop();
        }
        source->Release();
    }
}

IMediaSource* SourceManager::GetSource(SourceHandle handle)
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    auto it = m_sources.find(handle);
    if (it == m_sources.end())
    {
        return nullptr;
    }
    
    return it->second.source;
}

std::vector<IVideoSource*> SourceManager::GetVideoSources()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    std::vector<IVideoSource*> videoSources;
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (entry.source->GetSourceType() == MediaSourceType::Video)
        {
            videoSources.push_back(static_cast<IVideoSource*>(entry.source));
        }
    }
    
    return videoSources;
}

std::vector<IAudioSource*> SourceManager::GetAudioSources()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    std::vector<IAudioSource*> audioSources;
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (entry.source->GetSourceType() == MediaSourceType::Audio)
        {
            audioSources.push_back(static_cast<IAudioSource*>(entry.source));
        }
    }
    
    return audioSources;
}

bool SourceManager::StartAll()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    bool allSucceeded = true;
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (!entry.source->IsRunning())
        {
            if (!entry.source->Start())
            {
                allSucceeded = false;
                // Continue starting others even if one fails
            }
        }
    }
    
    return allSucceeded;
}

void SourceManager::StopAll()
{
    std::lock_guard<std::mutex> lock(m_mutex);
    
    for (const auto& [handle, entry] : m_sources)
    {
        if (entry.source->IsRunning())
        {
            entry.source->Stop();
        }
    }
}

size_t SourceManager::GetSourceCount() const
{
    std::lock_guard<std::mutex> lock(m_mutex);
    return m_sources.size();
}

void SourceManager::Clear()
{
    std::vector<IMediaSource*> sourcesToRelease;
    
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        // Collect all sources
        for (const auto& [handle, entry] : m_sources)
        {
            sourcesToRelease.push_back(entry.source);
        }
        
        m_sources.clear();
    }
    
    // Stop and release outside the lock to avoid deadlock
    for (auto* source : sourcesToRelease)
    {
        if (source->IsRunning())
        {
            source->Stop();
        }
        source->Release();
    }
}

SourceHandle SourceManager::GenerateHandle()
{
    // Already inside lock when called
    return m_nextHandle++;
}
