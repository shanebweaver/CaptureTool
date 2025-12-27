#pragma once

/// <summary>
/// Interface for managing Media Foundation initialization and shutdown lifecycle.
/// Provides abstraction for Media Foundation lifecycle management to enable dependency injection and testing.
/// </summary>
class IMediaFoundationLifecycleManager
{
public:
    virtual ~IMediaFoundationLifecycleManager() = default;

    /// <summary>
    /// Check if Media Foundation was successfully initialized.
    /// </summary>
    /// <returns>True if MFStartup succeeded, false otherwise.</returns>
    virtual bool IsInitialized() const = 0;

    /// <summary>
    /// Get the HRESULT from Media Foundation initialization.
    /// </summary>
    /// <returns>S_OK if initialization succeeded, error HRESULT otherwise.</returns>
    virtual long GetInitializationResult() const = 0;
};
