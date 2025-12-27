#pragma once
#include <atomic>

/// <summary>
/// Manages the lifecycle of Media Foundation (MF) initialization and shutdown.
/// Ensures MFStartup is called before any MF operations and MFShutdown on cleanup.
/// Thread-safe reference counting allows multiple components to share MF resources.
/// 
/// Implements RUST Principles:
/// - Principle #5 (RAII Everything): MFStartup in constructor, MFShutdown in destructor
/// - Principle #6 (No Globals): Instance-based lifecycle management
/// - Principle #8 (Thread Safety): Atomic reference counting
/// </summary>
class MediaFoundationLifecycleManager
{
public:
    /// <summary>
    /// Initialize Media Foundation. Safe to call multiple times.
    /// </summary>
    MediaFoundationLifecycleManager();

    /// <summary>
    /// Shutdown Media Foundation if this is the last reference.
    /// </summary>
    ~MediaFoundationLifecycleManager();

    // Delete copy operations (resource cannot be copied)
    MediaFoundationLifecycleManager(const MediaFoundationLifecycleManager&) = delete;
    MediaFoundationLifecycleManager& operator=(const MediaFoundationLifecycleManager&) = delete;

    // Allow move operations
    MediaFoundationLifecycleManager(MediaFoundationLifecycleManager&& other) noexcept;
    MediaFoundationLifecycleManager& operator=(MediaFoundationLifecycleManager&& other) noexcept;

    /// <summary>
    /// Check if Media Foundation was successfully initialized.
    /// </summary>
    bool IsInitialized() const { return m_initialized; }

    /// <summary>
    /// Get the HRESULT from initialization attempt.
    /// </summary>
    long GetInitializationResult() const { return m_initHr; }

private:
    bool m_initialized;
    long m_initHr;
    
    // Global reference counter for MF lifecycle
    static std::atomic<int> s_refCount;
};
