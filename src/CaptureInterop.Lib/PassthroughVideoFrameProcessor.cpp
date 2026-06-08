#include "pch.h"
#include "PassthroughVideoFrameProcessor.h"

Result<VideoFrameProcessorResult> PassthroughVideoFrameProcessor::Process(ID3D11Texture2D* texture)
{
    if (!texture)
    {
        return Result<VideoFrameProcessorResult>::Error(
            ErrorInfo::FromMessage(E_INVALIDARG, "Texture is null", "PassthroughVideoFrameProcessor::Process"));
    }

    return Result<VideoFrameProcessorResult>::Ok(VideoFrameProcessorResult{ texture });
}
