#pragma once
#include "IStreamConfigurationBuilder.h"
#include <memory>

/// <summary>
/// Factory interface for creating stream configuration builder instances.
/// Provides abstraction for stream configuration builder creation to enable dependency injection and testing.
/// </summary>
class IStreamConfigurationBuilderFactory
{
public:
    virtual ~IStreamConfigurationBuilderFactory() = default;

    /// <summary>
    /// Create a new stream configuration builder instance.
    /// </summary>
    /// <returns>A unique pointer to a new IStreamConfigurationBuilder implementation.</returns>
    virtual std::unique_ptr<IStreamConfigurationBuilder> CreateConfigurationBuilder() = 0;
};
