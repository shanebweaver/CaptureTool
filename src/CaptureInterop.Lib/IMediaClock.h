#pragma once
#include "IMediaClockReader.h"
#include "IMediaClockController.h"
#include "IMediaClockWriter.h"

/// <summary>
/// Unified interface for MediaClock implementations.
/// Combines reader, controller, and writer interfaces for a complete media clock abstraction.
/// 
/// This interface allows different implementations of media clock synchronization
/// while maintaining a consistent API for:
/// - Reading current media time (IMediaClockReader)
/// - Controlling clock lifecycle (IMediaClockController)
/// - Advancing clock time (IMediaClockWriter)
/// 
/// Implementations must be thread-safe and support concurrent access from
/// audio and video capture threads.
/// </summary>
class IMediaClock : public IMediaClockReader, public IMediaClockController, public IMediaClockWriter
{
public:
    ~IMediaClock() override = default;
};
