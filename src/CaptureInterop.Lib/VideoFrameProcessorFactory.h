#pragma once

#include "IVideoFrameProcessorFactory.h"

class VideoFrameProcessorFactory final : public IVideoFrameProcessorFactory
{
public:
    Result<std::unique_ptr<IVideoFrameProcessor>> CreateProcessor(const VideoFrameProcessorFactoryContext& context) override;
};
