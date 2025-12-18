#pragma once

/// <summary>
/// Type of media source (video or audio).
/// </summary>
enum class MediaSourceType
{
    Video,
    Audio
};

/// <summary>
/// Base interface for all capture sources.
/// Provides common lifecycle management and type identification.
/// All sources implement reference counting for memory management.
/// </summary>
class IMediaSource
{
public:
    virtual ~IMediaSource() = default;
    
    /// <summary>
    /// Get the type of this media source.
    /// </summary>
    /// <returns>MediaSourceType indicating if this is a video or audio source.</returns>
    virtual MediaSourceType GetSourceType() const = 0;
    
    /// <summary>
    /// Initialize the source with necessary resources.
    /// Must be called before Start().
    /// </summary>
    /// <returns>True if initialization succeeded, false otherwise.</returns>
    virtual bool Initialize() = 0;
    
    /// <summary>
    /// Start capturing from this source.
    /// Source must be initialized before calling this.
    /// </summary>
    /// <returns>True if capture started successfully, false otherwise.</returns>
    virtual bool Start() = 0;
    
    /// <summary>
    /// Stop capturing from this source.
    /// Safe to call multiple times. Resources remain allocated until Release().
    /// </summary>
    virtual void Stop() = 0;
    
    /// <summary>
    /// Check if the source is currently capturing.
    /// </summary>
    /// <returns>True if capture is active, false otherwise.</returns>
    virtual bool IsRunning() const = 0;
    
    /// <summary>
    /// Increment reference count.
    /// </summary>
    /// <returns>New reference count.</returns>
    virtual ULONG AddRef() = 0;
    
    /// <summary>
    /// Decrement reference count and delete if it reaches zero.
    /// </summary>
    /// <returns>New reference count (0 if object was deleted).</returns>
    virtual ULONG Release() = 0;
};
