#pragma once
#include "IMediaFoundationLifecycleManager.h"
#include <memory>

/// <summary>
/// Factory interface for creating Media Foundation lifecycle manager instances.
/// Provides abstraction for lifecycle manager creation to enable dependency injection and testing.
/// </summary>
class IMediaFoundationLifecycleManagerFactory
{
public:
    virtual ~IMediaFoundationLifecycleManagerFactory() = default;

    /// <summary>
    /// Create a new Media Foundation lifecycle manager instance.
    /// </summary>
    /// <returns>A unique pointer to a new IMediaFoundationLifecycleManager implementation.</returns>
    virtual std::unique_ptr<IMediaFoundationLifecycleManager> CreateLifecycleManager() = 0;
};
