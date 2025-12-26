#pragma once
#include <atomic>

/// <summary>
/// Explicit state machine for capture session lifecycle.
/// Defines valid states and transitions for compile-time safety.
/// </summary>
enum class CaptureSessionState
{
    /// <summary>
    /// Session created but not initialized. Initial state.
    /// Valid transitions: Initialized, Failed
    /// </summary>
    Created,
    
    /// <summary>
    /// Dependencies initialized, ready to start.
    /// Valid transitions: Active, Failed
    /// </summary>
    Initialized,
    
    /// <summary>
    /// Actively capturing audio/video.
    /// Valid transitions: Paused, Stopped, Failed
    /// </summary>
    Active,
    
    /// <summary>
    /// Capture paused, can resume.
    /// Valid transitions: Active, Stopped, Failed
    /// </summary>
    Paused,
    
    /// <summary>
    /// Capture stopped cleanly. Terminal state.
    /// Valid transitions: (none)
    /// </summary>
    Stopped,
    
    /// <summary>
    /// Error occurred, session unusable. Terminal state.
    /// Valid transitions: (none)
    /// </summary>
    Failed
};

/// <summary>
/// State machine that validates transitions and provides query methods.
/// Thread-safe using atomic operations with appropriate memory ordering.
/// </summary>
class CaptureSessionStateMachine
{
public:
    /// <summary>
    /// Initialize state machine in Created state.
    /// </summary>
    CaptureSessionStateMachine() : m_state(CaptureSessionState::Created) {}
    
    /// <summary>
    /// Get the current state.
    /// Uses acquire memory ordering to ensure visibility of state changes.
    /// </summary>
    CaptureSessionState GetState() const 
    { 
        return m_state.load(std::memory_order_acquire); 
    }
    
    /// <summary>
    /// Check if the session is initialized (not Created and not Failed).
    /// </summary>
    bool IsInitialized() const 
    { 
        auto s = m_state.load(std::memory_order_acquire);
        return s != CaptureSessionState::Created && s != CaptureSessionState::Failed;
    }
    
    /// <summary>
    /// Check if the session is active (Active or Paused state).
    /// Used to determine if Stop() is valid.
    /// </summary>
    bool IsActive() const 
    { 
        auto s = m_state.load(std::memory_order_acquire);
        return s == CaptureSessionState::Active || s == CaptureSessionState::Paused;
    }
    
    /// <summary>
    /// Check if a transition to the specified state is valid from the current state.
    /// Does not modify state.
    /// </summary>
    bool CanTransitionTo(CaptureSessionState newState) const
    {
        return IsValidTransition(m_state.load(std::memory_order_acquire), newState);
    }
    
    /// <summary>
    /// Attempt to transition to the specified state.
    /// Uses release memory ordering to ensure all prior writes are visible.
    /// </summary>
    /// <returns>True if transition succeeded, false if invalid transition.</returns>
    bool TryTransitionTo(CaptureSessionState newState)
    {
        auto currentState = m_state.load(std::memory_order_acquire);
        
        if (!IsValidTransition(currentState, newState))
            return false;
            
        m_state.store(newState, std::memory_order_release);
        return true;
    }
    
private:
    std::atomic<CaptureSessionState> m_state;
    
    /// <summary>
    /// Determine if a state transition is valid according to the state machine rules.
    /// </summary>
    static bool IsValidTransition(CaptureSessionState from, CaptureSessionState to)
    {
        switch (from)
        {
        case CaptureSessionState::Created:
            return to == CaptureSessionState::Initialized || to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Initialized:
            return to == CaptureSessionState::Active || to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Active:
            return to == CaptureSessionState::Paused || 
                   to == CaptureSessionState::Stopped || 
                   to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Paused:
            return to == CaptureSessionState::Active || 
                   to == CaptureSessionState::Stopped || 
                   to == CaptureSessionState::Failed;
            
        case CaptureSessionState::Stopped:
        case CaptureSessionState::Failed:
            return false; // Terminal states - no transitions allowed
            
        default:
            return false;
        }
    }
};
