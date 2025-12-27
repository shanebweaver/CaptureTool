#pragma once
#include "ISampleBuilder.h"
#include <memory>

/// <summary>
/// Factory interface for creating sample builder instances.
/// Provides abstraction for sample builder creation to enable dependency injection and testing.
/// </summary>
class ISampleBuilderFactory
{
public:
    virtual ~ISampleBuilderFactory() = default;

    /// <summary>
    /// Create a new sample builder instance.
    /// </summary>
    /// <returns>A unique pointer to a new ISampleBuilder implementation.</returns>
    virtual std::unique_ptr<ISampleBuilder> CreateSampleBuilder() = 0;
};
