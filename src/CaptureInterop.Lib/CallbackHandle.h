#pragma once
#include <memory>
#include <atomic>
#include <functional>

namespace CaptureInterop {

/// <summary>
/// Handle for a registered callback that automatically unregisters on destruction.
/// Provides RAII for callback lifetime management.
/// This is an internal implementation detail - external API still uses raw function pointers.
/// </summary>
class CallbackHandle
{
public:
    CallbackHandle() = default;
    
    /// <summary>
    /// Create handle with unregister function.
    /// </summary>
    explicit CallbackHandle(std::function<void()> unregisterFn)
        : m_unregister(std::make_shared<UnregisterToken>(std::move(unregisterFn)))
    {}
    
    // Move-only type
    CallbackHandle(CallbackHandle&&) = default;
    CallbackHandle& operator=(CallbackHandle&&) = default;
    CallbackHandle(const CallbackHandle&) = delete;
    CallbackHandle& operator=(const CallbackHandle&) = delete;
    
    /// <summary>
    /// Explicit unregister (also happens automatically on destruction).
    /// Safe to call multiple times.
    /// </summary>
    void Unregister()
    {
        if (m_unregister)
        {
            m_unregister->Unregister();
            m_unregister.reset();
        }
    }
    
    /// <summary>
    /// Check if the handle is valid (not yet unregistered).
    /// </summary>
    bool IsValid() const
    {
        return m_unregister != nullptr;
    }
    
private:
    struct UnregisterToken
    {
        explicit UnregisterToken(std::function<void()> fn)
            : unregisterFn(std::move(fn)) {}
            
        ~UnregisterToken()
        {
            Unregister();
        }
        
        void Unregister()
        {
            if (!called.exchange(true))
            {
                unregisterFn();
            }
        }
        
        std::atomic<bool> called{false};
        std::function<void()> unregisterFn;
    };
    
    std::shared_ptr<UnregisterToken> m_unregister;
};

} // namespace CaptureInterop
