#include "pch.h"
#include "MediaFoundationLifecycleManager.h"

// Initialize static reference counter
std::atomic<int> MediaFoundationLifecycleManager::s_refCount{0};

MediaFoundationLifecycleManager::MediaFoundationLifecycleManager()
    : m_initialized(false)
    , m_initHr(S_OK)
{
    int prevCount = s_refCount.fetch_add(1, std::memory_order_acq_rel);
    
    if (prevCount == 0)
    {
        m_initHr = MFStartup(MF_VERSION);
        m_initialized = SUCCEEDED(m_initHr);
    }
    else
    {
        m_initialized = true;
        m_initHr = S_OK;
    }
}

MediaFoundationLifecycleManager::~MediaFoundationLifecycleManager()
{
    if (m_initialized)
    {
        int prevCount = s_refCount.fetch_sub(1, std::memory_order_acq_rel);
        
        if (prevCount == 1)
        {
            MFShutdown();
        }
    }
}
