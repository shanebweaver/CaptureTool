#include "pch.h"
#include "StreamConfigurationBuilderFactory.h"
#include "StreamConfigurationBuilder.h"

std::unique_ptr<IStreamConfigurationBuilder> StreamConfigurationBuilderFactory::CreateConfigurationBuilder()
{
    return std::make_unique<StreamConfigurationBuilder>();
}
