#include "pch.h"
#include "MediaFoundationLifecycleManager.h"

// Initialize static reference counter
std::atomic<int> MediaFoundationLifecycleManager::s_refCount{0};

MediaFoundationLifecycleManager::MediaFoundationLifecycleManager()
    : m_initialized(false)
    , m_initHr(S_OK)
{
    // Increment reference count with acquire-release semantics
    // This ensures proper synchronization of MF initialization state across threads
    int prevCount = s_refCount.fetch_add(1, std::memory_order_acq_rel);
    
    // Only initialize MF if this is the first instance
    if (prevCount == 0)
    {
        m_initHr = MFStartup(MF_VERSION);
        m_initialized = SUCCEEDED(m_initHr);
    }
    else
    {
        // MF is already initialized
        m_initialized = true;
        m_initHr = S_OK;
    }
}

MediaFoundationLifecycleManager::~MediaFoundationLifecycleManager()
{
    // Only shutdown if we were successfully initialized
    if (m_initialized)
    {
        // Decrement reference count with acquire-release semantics
        // This ensures proper synchronization when determining if MFShutdown should be called
        int prevCount = s_refCount.fetch_sub(1, std::memory_order_acq_rel);
        
        // Only shutdown MF if this is the last instance
        if (prevCount == 1)
        {
            MFShutdown();
        }
    }
}

MediaFoundationLifecycleManager::MediaFoundationLifecycleManager(MediaFoundationLifecycleManager&& other) noexcept
    : m_initialized(other.m_initialized)
    , m_initHr(other.m_initHr)
{
    // Mark other as moved-from
    other.m_initialized = false;
}

MediaFoundationLifecycleManager& MediaFoundationLifecycleManager::operator=(MediaFoundationLifecycleManager&& other) noexcept
{
    if (this != &other)
    {
        // Clean up current state
        if (m_initialized)
        {
            int prevCount = s_refCount.fetch_sub(1, std::memory_order_acq_rel);
            if (prevCount == 1)
            {
                MFShutdown();
            }
        }
        
        // Move from other
        m_initialized = other.m_initialized;
        m_initHr = other.m_initHr;
        
        // Mark other as moved-from
        other.m_initialized = false;
    }
    return *this;
}
