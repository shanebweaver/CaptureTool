#pragma once
#include "IMediaSource.h"
#include "IVideoSource.h"
#include "IAudioSource.h"
#include <vector>
#include <unordered_map>
#include <mutex>
#include <cstdint>

/// <summary>
/// Handle type for identifying registered sources.
/// </summary>
typedef uint64_t SourceHandle;

/// <summary>
/// Invalid source handle value.
/// </summary>
constexpr SourceHandle INVALID_SOURCE_HANDLE = 0;

/// <summary>
/// Manages lifecycle and coordination of multiple capture sources.
/// Thread-safe singleton for global source management.
/// </summary>
class SourceManager
{
public:
    /// <summary>
    /// Get the singleton instance.
    /// </summary>
    static SourceManager& Instance();
    
    /// <summary>
    /// Register a new source.
    /// Takes a reference to the source (calls AddRef).
    /// </summary>
    /// <param name="source">Source to register. Must not be null.</param>
    /// <returns>Handle to the registered source, or INVALID_SOURCE_HANDLE on failure.</returns>
    SourceHandle RegisterSource(IMediaSource* source);
    
    /// <summary>
    /// Unregister and release a source.
    /// Safe to call even if source is running (will stop first).
    /// </summary>
    /// <param name="handle">Handle of source to unregister.</param>
    void UnregisterSource(SourceHandle handle);
    
    /// <summary>
    /// Get a source by handle.
    /// </summary>
    /// <param name="handle">Source handle.</param>
    /// <returns>Pointer to source, or nullptr if handle invalid.</returns>
    IMediaSource* GetSource(SourceHandle handle);
    
    /// <summary>
    /// Get all video sources.
    /// </summary>
    std::vector<IVideoSource*> GetVideoSources();
    
    /// <summary>
    /// Get all audio sources.
    /// </summary>
    std::vector<IAudioSource*> GetAudioSources();
    
    /// <summary>
    /// Start all registered sources.
    /// </summary>
    /// <returns>True if all sources started successfully.</returns>
    bool StartAll();
    
    /// <summary>
    /// Stop all registered sources.
    /// </summary>
    void StopAll();
    
    /// <summary>
    /// Get count of registered sources.
    /// </summary>
    size_t GetSourceCount() const;
    
    /// <summary>
    /// Clear all sources (stops and unregisters).
    /// </summary>
    void Clear();

private:
    SourceManager() = default;
    ~SourceManager();
    
    // Non-copyable
    SourceManager(const SourceManager&) = delete;
    SourceManager& operator=(const SourceManager&) = delete;
    
    struct SourceEntry
    {
        IMediaSource* source;
        SourceHandle handle;
    };
    
    std::unordered_map<SourceHandle, SourceEntry> m_sources;
    mutable std::mutex m_mutex;
    SourceHandle m_nextHandle = 1;
    
    SourceHandle GenerateHandle();
};
