#pragma once
#include "IMediaSource.h"
#include <functional>
#include <d3d11.h>

/// <summary>
/// Callback signature for video frame delivery.
/// Called on capture thread when a new frame is available.
/// </summary>
/// <param name="texture">D3D11 texture containing the frame data.</param>
/// <param name="timestamp">Timestamp in 100-nanosecond units (relative to recording start).</param>
using VideoFrameCallback = std::function<void(ID3D11Texture2D* texture, LONGLONG timestamp)>;

/// <summary>
/// Interface for video capture sources.
/// Extends IMediaSource with video-specific functionality.
/// </summary>
class IVideoSource : public IMediaSource
{
public:
    /// <summary>
    /// Get the resolution of captured video frames.
    /// </summary>
    /// <param name="width">Output parameter for frame width in pixels.</param>
    /// <param name="height">Output parameter for frame height in pixels.</param>
    virtual void GetResolution(UINT32& width, UINT32& height) const = 0;
    
    /// <summary>
    /// Set the callback to receive captured video frames.
    /// Must be set before Start() to receive frames.
    /// </summary>
    /// <param name="callback">Function to call for each captured frame.</param>
    virtual void SetFrameCallback(VideoFrameCallback callback) = 0;
    
    /// <summary>
    /// Override to return Video type.
    /// </summary>
    MediaSourceType GetSourceType() const override { return MediaSourceType::Video; }
};
