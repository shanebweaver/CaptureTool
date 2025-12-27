#pragma once
#include "ISampleBuilderFactory.h"

/// <summary>
/// Factory for creating sample builder instances.
/// </summary>
class SampleBuilderFactory : public ISampleBuilderFactory
{
public:
    SampleBuilderFactory() = default;
    ~SampleBuilderFactory() override = default;

    // ISampleBuilderFactory implementation
    std::unique_ptr<ISampleBuilder> CreateSampleBuilder() override;
};
