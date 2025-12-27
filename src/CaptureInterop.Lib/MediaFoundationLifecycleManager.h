#pragma once
#include <atomic>

/// <summary>
/// Manages Media Foundation initialization and shutdown with thread-safe reference counting.
/// RAII pattern ensures MFStartup is called in constructor and MFShutdown in destructor.
/// </summary>
class MediaFoundationLifecycleManager
{
public:
    MediaFoundationLifecycleManager();
    ~MediaFoundationLifecycleManager();

    // Non-copyable, non-movable (shares global MF state)
    MediaFoundationLifecycleManager(const MediaFoundationLifecycleManager&) = delete;
    MediaFoundationLifecycleManager& operator=(const MediaFoundationLifecycleManager&) = delete;
    MediaFoundationLifecycleManager(MediaFoundationLifecycleManager&&) = delete;
    MediaFoundationLifecycleManager& operator=(MediaFoundationLifecycleManager&&) = delete;

    bool IsInitialized() const { return m_initialized; }
    long GetInitializationResult() const { return m_initHr; }

private:
    bool m_initialized;
    long m_initHr;
    
    // Global reference counter for MF lifecycle
    static std::atomic<int> s_refCount;
};
