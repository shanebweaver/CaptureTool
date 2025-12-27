#pragma once
#include "IStreamConfigurationBuilderFactory.h"

/// <summary>
/// Factory for creating stream configuration builder instances.
/// </summary>
class StreamConfigurationBuilderFactory : public IStreamConfigurationBuilderFactory
{
public:
    StreamConfigurationBuilderFactory() = default;
    ~StreamConfigurationBuilderFactory() override = default;

    // IStreamConfigurationBuilderFactory implementation
    std::unique_ptr<IStreamConfigurationBuilder> CreateConfigurationBuilder() override;
};
