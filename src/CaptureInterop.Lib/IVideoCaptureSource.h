#pragma once
#include <functional>

/// <summary>
/// Event arguments for video frame ready event.
/// Contains the video frame data and timing information.
/// </summary>
struct VideoFrameReadyEventArgs
{
    ID3D11Texture2D* pTexture;  // Pointer to the D3D11 texture containing the frame
    LONGLONG timestamp;         // Timestamp for this frame (100ns ticks)
};

/// <summary>
/// Callback function type for video frame ready events.
/// </summary>
using VideoFrameReadyCallback = std::function<void(const VideoFrameReadyEventArgs&)>;

/// <summary>
/// Interface for video capture sources that can be captured and written to an output stream.
/// Implementations provide different video sources (screen capture, window capture, etc.)
/// </summary>
class IVideoCaptureSource
{
public:
    virtual ~IVideoCaptureSource() = default;

    /// <summary>
    /// Initialize the video capture source.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    virtual bool Initialize(HRESULT* outHr = nullptr) = 0;

    /// <summary>
    /// Start capturing video from the capture source.
    /// </summary>
    /// <param name="outHr">Optional pointer to receive the HRESULT error code.</param>
    /// <returns>True if capture started successfully, false otherwise.</returns>
    virtual bool Start(HRESULT* outHr = nullptr) = 0;

    /// <summary>
    /// Stop capturing video from the capture source.
    /// </summary>
    virtual void Stop() = 0;

    /// <summary>
    /// Get the width of the video frames.
    /// </summary>
    /// <returns>Frame width in pixels, or 0 if not initialized.</returns>
    virtual UINT32 GetWidth() const = 0;

    /// <summary>
    /// Get the height of the video frames.
    /// </summary>
    /// <returns>Frame height in pixels, or 0 if not initialized.</returns>
    virtual UINT32 GetHeight() const = 0;

    /// <summary>
    /// Set the callback to be invoked when a video frame is ready.
    /// The callback is invoked on a background processing thread.
    /// </summary>
    /// <param name="callback">Callback function to receive video frames.</param>
    virtual void SetVideoFrameReadyCallback(VideoFrameReadyCallback callback) = 0;

    /// <summary>
    /// Check if the video capture source is currently running.
    /// </summary>
    /// <returns>True if capture is active, false otherwise.</returns>
    virtual bool IsRunning() const = 0;
};
