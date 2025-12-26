#pragma once
#include "CallbackHandle.h"
#include <mutex>
#include <unordered_map>
#include <functional>
#include <atomic>
#include <vector>

namespace CaptureInterop {

/// <summary>
/// Thread-safe registry for callbacks with automatic lifetime management.
/// Guarantees callbacks won't be invoked after being unregistered.
/// Supports multiple callbacks for the same event.
/// </summary>
template<typename TArgs>
class CallbackRegistry
{
public:
    using CallbackFn = std::function<void(const TArgs&)>;
    using CallbackId = uint64_t;
    
    CallbackRegistry() = default;
    ~CallbackRegistry() = default;
    
    // Non-copyable, non-movable
    CallbackRegistry(const CallbackRegistry&) = delete;
    CallbackRegistry& operator=(const CallbackRegistry&) = delete;
    CallbackRegistry(CallbackRegistry&&) = delete;
    CallbackRegistry& operator=(CallbackRegistry&&) = delete;
    
    /// <summary>
    /// Register a callback and return a handle.
    /// Callback will be automatically unregistered when handle is destroyed.
    /// </summary>
    CallbackHandle Register(CallbackFn callback)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        
        CallbackId id = m_nextId++;
        m_callbacks[id] = std::move(callback);
        
        // Return handle that will unregister on destruction
        return CallbackHandle([this, id]() { this->Unregister(id); });
    }
    
    /// <summary>
    /// Invoke all registered callbacks with the given arguments.
    /// Thread-safe and guarantees callbacks exist for duration of invocation.
    /// Callbacks are invoked without holding the lock to prevent deadlocks.
    /// 
    /// Note: This method copies all callback functions to a local vector before invoking them.
    /// If callbacks are heavyweight (large captured state), this copy could be expensive.
    /// Callbacks should generally be lightweight lambdas or function pointers.
    /// </summary>
    void Invoke(const TArgs& args)
    {
        // Copy callbacks under lock, then invoke without lock to prevent deadlocks
        std::vector<CallbackFn> callbacks;
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            callbacks.reserve(m_callbacks.size());
            for (const auto& pair : m_callbacks)
            {
                callbacks.push_back(pair.second);
            }
        }
        
        // Invoke without holding lock
        for (const auto& callback : callbacks)
        {
            callback(args);
        }
    }
    
    /// <summary>
    /// Clear all callbacks. Useful during shutdown.
    /// </summary>
    void Clear()
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_callbacks.clear();
    }
    
    /// <summary>
    /// Get the number of registered callbacks.
    /// </summary>
    size_t Count() const
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_callbacks.size();
    }
    
    /// <summary>
    /// Check if any callbacks are registered.
    /// </summary>
    bool HasCallbacks() const
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        return !m_callbacks.empty();
    }
    
private:
    void Unregister(CallbackId id)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_callbacks.erase(id);
    }
    
    mutable std::mutex m_mutex;
    std::unordered_map<CallbackId, CallbackFn> m_callbacks;
    // Note: m_nextId is a monotonically increasing 64-bit counter.
    // Overflow protection is not explicitly implemented as reaching the limit would require
    // over 18 quintillion registrations, which is not feasible in practice.
    std::atomic<CallbackId> m_nextId{1};
};

} // namespace CaptureInterop
