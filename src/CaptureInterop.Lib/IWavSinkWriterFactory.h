#pragma once
#include "IWavSinkWriter.h"
#include <memory>

class IWavSinkWriterFactory
{
public:
    virtual ~IWavSinkWriterFactory() = default;

    virtual std::unique_ptr<IWavSinkWriter> CreateSinkWriter() = 0;
};
