#include "pch.h"
#include "WindowsWaveSinkWriterFactory.h"
#include "WindowsWaveSinkWriter.h"

std::unique_ptr<IWavSinkWriter> WindowsWaveSinkWriterFactory::CreateSinkWriter()
{
    return std::make_unique<WindowsWaveSinkWriter>();
}
