#pragma once
#include "IMP4SinkWriterFactory.h"

/// <summary>
/// Factory for creating Windows Media Foundation MP4 sink writer instances.
/// </summary>
class WindowsMFMP4SinkWriterFactory : public IMP4SinkWriterFactory
{
public:
    WindowsMFMP4SinkWriterFactory() = default;
    ~WindowsMFMP4SinkWriterFactory() override = default;

    // IMP4SinkWriterFactory implementation
    std::unique_ptr<IMP4SinkWriter> CreateSinkWriter() override;
};
