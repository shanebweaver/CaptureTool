#pragma once
#include "IWavSinkWriterFactory.h"

class WindowsWaveSinkWriterFactory : public IWavSinkWriterFactory
{
public:
    WindowsWaveSinkWriterFactory() = default;
    ~WindowsWaveSinkWriterFactory() override = default;

    std::unique_ptr<IWavSinkWriter> CreateSinkWriter() override;
};
