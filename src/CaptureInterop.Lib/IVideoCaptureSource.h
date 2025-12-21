#pragma once

// Forward declarations
class MP4SinkWriter;
class IMediaClockWriter;

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
    /// Set the MP4 sink writer to receive captured video frames.
    /// Must be called before Start() to enable audio/video synchronization.
    /// </summary>
    /// <param name="sinkWriter">Pointer to the MP4SinkWriter instance.</param>
    virtual void SetSinkWriter(MP4SinkWriter* sinkWriter) = 0;

    /// <summary>
    /// Set the media clock writer for timestamp synchronization.
    /// Must be called before Start() for proper A/V sync.
    /// </summary>
    /// <param name="clockWriter">Pointer to the IMediaClockWriter instance.</param>
    virtual void SetMediaClockWriter(IMediaClockWriter* clockWriter) = 0;

    /// <summary>
    /// Check if the video capture source is currently running.
    /// </summary>
    /// <returns>True if capture is active, false otherwise.</returns>
    virtual bool IsRunning() const = 0;
};
