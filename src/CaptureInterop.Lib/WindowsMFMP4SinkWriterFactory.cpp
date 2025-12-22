#include "pch.h"
#include "WindowsMFMP4SinkWriterFactory.h"
#include "WindowsMFMP4SinkWriter.h"

std::unique_ptr<IMP4SinkWriter> WindowsMFMP4SinkWriterFactory::CreateSinkWriter()
{
    return std::make_unique<WindowsMFMP4SinkWriter>();
}
