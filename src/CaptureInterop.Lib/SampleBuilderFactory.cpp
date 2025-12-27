#include "pch.h"
#include "SampleBuilderFactory.h"
#include "SampleBuilder.h"

std::unique_ptr<ISampleBuilder> SampleBuilderFactory::CreateSampleBuilder()
{
    return std::make_unique<SampleBuilder>();
}
