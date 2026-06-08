#pragma once

#include "IVideoFrameProcessor.h"
#include "MonitorHdrInfo.h"
#include "Result.h"

#include <cstdint>
#include <memory>

struct ID3D11Device;

struct VideoFrameProcessorFactoryContext
{
    MonitorHdrInfo monitorHdrInfo;
    ID3D11Device* device;
    uint32_t width;
    uint32_t height;
};

class IVideoFrameProcessorFactory
{
public:
    virtual ~IVideoFrameProcessorFactory() = default;

    virtual Result<std::unique_ptr<IVideoFrameProcessor>> CreateProcessor(const VideoFrameProcessorFactoryContext& context) = 0;
};
