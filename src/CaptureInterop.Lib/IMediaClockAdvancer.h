#pragma once

// Forward declaration
class IMediaClockWriter;

/// <summary>
/// Interface for components that can advance the media clock timeline.
/// Implementations are responsible for driving clock advancement based on their
/// specific timing source (e.g., audio samples, video frames, external timecode).
/// 
/// This interface decouples the clock from specific implementation details,
/// allowing different types of clock advancers while maintaining clean architecture.
/// </summary>
class IMediaClockAdvancer
{
public:
    virtual ~IMediaClockAdvancer() = default;

    /// <summary>
    /// Set the media clock writer that will be advanced by this component.
    /// The advancer calls methods on the clock writer to update the timeline
    /// as media samples are processed.
    /// Must be called before starting capture to enable clock synchronization.
    /// </summary>
    /// <param name="clockWriter">Pointer to the IMediaClockWriter instance.</param>
    virtual void SetClockWriter(IMediaClockWriter* clockWriter) = 0;
};
