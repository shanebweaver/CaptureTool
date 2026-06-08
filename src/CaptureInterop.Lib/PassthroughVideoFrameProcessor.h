#pragma once

#include "IVideoFrameProcessor.h"

class PassthroughVideoFrameProcessor final : public IVideoFrameProcessor
{
public:
    Result<VideoFrameProcessorResult> Process(ID3D11Texture2D* texture) override;
};
