#pragma once

#include "Result.h"

struct ID3D11Texture2D;

struct VideoFrameProcessorResult
{
    ID3D11Texture2D* texture;
};

class IVideoFrameProcessor
{
public:
    virtual ~IVideoFrameProcessor() = default;

    virtual Result<VideoFrameProcessorResult> Process(ID3D11Texture2D* texture) = 0;
    virtual void Reset() {}
};
