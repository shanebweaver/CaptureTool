#pragma once
#include "IMediaFoundationLifecycleManagerFactory.h"

/// <summary>
/// Factory for creating Media Foundation lifecycle manager instances.
/// </summary>
class MediaFoundationLifecycleManagerFactory : public IMediaFoundationLifecycleManagerFactory
{
public:
    MediaFoundationLifecycleManagerFactory() = default;
    ~MediaFoundationLifecycleManagerFactory() override = default;

    // IMediaFoundationLifecycleManagerFactory implementation
    std::unique_ptr<IMediaFoundationLifecycleManager> CreateLifecycleManager() override;
};
